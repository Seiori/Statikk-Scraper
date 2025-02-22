using System.Text.Json;
using Camille.Enums;
using Camille.RiotGames;
using Camille.RiotGames.LeagueExpV4;
using Camille.RiotGames.MatchV5;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data;
using Statikk_Scraper.Helpers;
using Statikk_Scraper.Models;


namespace Statikk_Scraper;

public static class DataRoutine
{
    private static RiotGamesApi? _riotGamesApi;
    private static string _connectionString = string.Empty;
    
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - 86400;
    
    private static readonly Dictionary<PlatformRoute, RegionalRoute> Regions = new()
    {
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE,
    };
    
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];
    
    private static MosgiContext CreateSeioriContext() => new(new DbContextOptionsBuilder<MosgiContext>().UseSqlServer(_connectionString).Options);
    
    public static async Task BeginDataRoutine()
    {
        _riotGamesApi = RiotGamesApi.NewInstance(
            new RiotGamesApiConfig.Builder(Environment.GetEnvironmentVariable("API_KEY") ?? throw new Exception("API_KEY is not set"))
            {
                Retries = 10,
                MaxConcurrentRequests = 5000,
            }.Build());
        
        _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new Exception("CONNECTION_STRING is not set");

        var totalMatches = 0;
        var page = 1;
        var currentTier = Tier.NONE;
        var currentDivision = Division.NONE;

        try
        {
            await using var context = CreateSeioriContext();

            foreach (var tier in Tiers)
            {
                currentTier = tier;

                foreach (var division in Divisions)
                {
                    currentDivision = division;
                    
                    page = 1;
                    
                    while (true)
                    {
                        var rankedData = await Task.WhenAll(
                            Regions.Keys.Select(region =>
                                FetchRankedData(region, Regions[region], tier, division, page)
                            )
                        );
                        
                        if (rankedData.FirstOrDefault().Item1 is null || rankedData.FirstOrDefault().Item2 is null)
                            break;
                        
                        var matchDataList = rankedData
                            .Select(data => data!.Item1)
                            .SelectMany(data => data!)
                            .ToArray();

                        var matchTimelineList = rankedData
                            .Select(data => data!.Item2)
                            .SelectMany(data => data!)
                            .ToArray();

                        if (matchDataList.Length == 0 || matchTimelineList.Length == 0)
                            break;

                        var matchesList = matchDataList
                            .Select(ModelHelpers.ParseRiotMatchData)
                            .Where(match => match is not null)
                            .ToArray();

                        var summonerDictionary = new Dictionary<int, Summoners>();
                        foreach (var match in matchesList)
                        {
                            foreach (var participant in match.Participants)
                            {
                                var summonerId = participant.Summoner.Id;
                                if (summonerDictionary.TryGetValue(summonerId, out var existingSummoner))
                                {
                                    // Use the existing Summoners instance
                                    participant.Summoner = existingSummoner;
                                }
                                else
                                {
                                    summonerDictionary[summonerId] = participant.Summoner;
                                }
                            }
                        }
                        
                        await context.BulkInsertOrUpdateAsync(matchesList, options =>
                        {
                            options.IncludeGraph = true;
                        });
                        
                        // await context.BulkInsertOrUpdateAsync(distinctSummoners.Values, options =>
                        // {
                        //     options.SetOutputIdentity = true;
                        //     options.UpdateByProperties = [nameof(Summoners.Puuid)];
                        // });
                        //
                        // await context.BulkInsertAsync(matchesList, options => options.SetOutputIdentity = true);
                        //
                        // foreach (var match in matchesList)
                        // {
                        //     foreach (var team in match!.Teams)
                        //         team.MatchesId = match.Id;
                        //
                        //     foreach (var participant in match.Participants)
                        //     {
                        //         participant.SummonersId = distinctSummoners[participant.Summoner.Puuid].Id;
                        //         participant.MatchesId = match.Id;
                        //     }
                        // }
                        //
                        // await context.BulkInsertAsync(matchesList.SelectMany(m => m!.Teams));
                        // await context.BulkInsertAsync(matchesList.SelectMany(m => m!.Participants));

                        Console.WriteLine("Imported {0} Matches for: {1} - {2} - Page {3} - {4}", matchesList.Length, tier, division, page, DateTime.Now);
                        
                        totalMatches += matchesList.Length;
                        page += 1;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("An Error Occurred in the Data Import Routine");
            Console.WriteLine("Current Tier: " + currentTier);
            Console.WriteLine("Current Division: " + currentDivision);
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }
    
    private static async Task<(IEnumerable<Match>, IEnumerable<Timeline>)> FetchRankedData(
        PlatformRoute platformRoute, RegionalRoute regionalRoute, Tier tier, Division division, int page)
    {
        await using var context = CreateSeioriContext();
        
        try
        {
            var leagueEntryList = await FetchLeagueEntriesAsync(platformRoute, tier, division, page);
            if (leagueEntryList is null) return (null, null)!;
            
            var summonerIdList = leagueEntryList
                .Select(le => le.SummonerId)
                .ToHashSet();
            
            var existingSummonersDict = await context.Summoners
                .AsNoTracking()
                .Where(s => s.Platform == platformRoute && summonerIdList.Contains(s.SummonerId))
                .Select(s => new { s.SummonerId, s.Puuid })
                .ToDictionaryAsync(s => s.SummonerId, s => s.Puuid);
            
            var missingSummonerIdList = summonerIdList
                .Except(existingSummonersDict.Keys)
                .ToHashSet();
            
            var fetchedPuuidList = await Task.WhenAll(
                missingSummonerIdList
                    .Select(id => FetchPuuidForSummonerId(platformRoute, id))
            );
            
            var puuidList = fetchedPuuidList
                .Where(puuid => !string.IsNullOrEmpty(puuid))
                .Concat(existingSummonersDict.Values)
                .ToHashSet();
            
            var matchIdLists = await Task.WhenAll(
                puuidList
                    .Select(puuid => FetchMatchIdListForPuuid(regionalRoute, puuid!))
            );
            
            var gameIdList = matchIdLists
                .SelectMany(matchIds => matchIds)
                .Select(id => long.Parse(id.Split('_')[1]))
                .ToHashSet();
            
            var existingGameIdList = await context.Matches
                .AsNoTracking()
                .Where(m => m.Platform == platformRoute && gameIdList.Contains(m.GameId))
                .Select(m => m.GameId)
                .ToHashSetAsync();
            
            var newGameIdList = gameIdList
                .Except(existingGameIdList)
                .ToHashSet();
            
            var matchDataList = await Task.WhenAll(
                newGameIdList
                    .Select(id => FetchMatchDataForMatchId(regionalRoute, $"{platformRoute}_{id}"))
            );
            
            var matchTimelineList = await Task.WhenAll(
                newGameIdList
                    .Select(id => FetchMatchTimelineForMatchId(regionalRoute, $"{platformRoute}_{id}"))
            );
            
            // Filter out any null results if necessary
            var filteredMatchDataList = matchDataList
                .Where(match => match is not null)
                .ToArray();
            
            var filteredMatchTimelineList = matchTimelineList
                .Where(timeline => timeline is not null)
                .ToArray();
            
            return (filteredMatchDataList, filteredMatchTimelineList);
        }
        catch (Exception e)
        {
            Console.WriteLine("An Error has Occurred. Please Check the Audit for More Information.");
            await context.AddAsync(
                new Audit()
                {
                    Method = nameof(FetchRankedData),
                    Input = JsonSerializer.Serialize(new { platformRoute, regionalRoute, tier, division, page }),
                    Exception = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty
                });
            
            await context.SaveChangesAsync();
        }
        
        return (null, null)!;
    }
    
    private static async Task<LeagueEntry[]?> FetchLeagueEntriesAsync(PlatformRoute platformRoute, Tier tier, Division division, int page)
    {
        if (tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && division is not Division.I) return [];
    
        return await _riotGamesApi!.LeagueExpV4().GetLeagueEntriesAsync(platformRoute, QueueType.RANKED_SOLO_5x5, tier, division, page);
    }
    
    private static async Task<string?> FetchPuuidForSummonerId(PlatformRoute platformRoute, string summonerId)
    {
        return (await _riotGamesApi!.SummonerV4().GetBySummonerIdAsync(platformRoute, summonerId)).Puuid;
    }
    
    private static async Task<string[]> FetchMatchIdListForPuuid(RegionalRoute regionalRoute, string puuid)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetMatchIdsByPUUIDAsync(
                regionalRoute, 
                puuid,
                100,
                queue: Queue.SUMMONERS_RIFT_5V5_RANKED_SOLO,
                startTime: StartTime,
                endTime: EndTime
            );
    }
    
    private static async Task<Match?> FetchMatchDataForMatchId(RegionalRoute regionalRoute, string matchId)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetMatchAsync(
                regionalRoute,
                matchId
            );
    }
    
    private static async Task<Timeline?> FetchMatchTimelineForMatchId(RegionalRoute regionalRoute, string matchId)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetTimelineAsync(
                regionalRoute,
                matchId
            );
    }
}