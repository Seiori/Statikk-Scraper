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

    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;

    private static readonly Dictionary<PlatformRoute, RegionalRoute> Regions = new()
    {
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE,
        [PlatformRoute.KR] = RegionalRoute.ASIA,
        [PlatformRoute.NA1] = RegionalRoute.AMERICAS,
    };

    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];

    private static MosgiContext CreateSeioriContext() => new(new DbContextOptionsBuilder<MosgiContext>().UseSqlServer(_connectionString).Options);

    public static async Task BeginDataRoutine(CancellationToken cancellationToken = default)
    {
        _riotGamesApi = RiotGamesApi.NewInstance(
            new RiotGamesApiConfig.Builder(Environment.GetEnvironmentVariable("API_KEY") ?? throw new Exception("API_KEY is not set"))
            {
                Retries = 10,
                MaxConcurrentRequests = 5000,
            }.Build());

        _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new Exception("CONNECTION_STRING is not set");

        await using var context = CreateSeioriContext();
        
        var totalMatches = 0;
        var page = 1;
        var currentTier = Tier.NONE;
        var currentDivision = Division.NONE;

        try
        {
            foreach (var tier in Tiers)
            {
                currentTier = tier;
                foreach (var division in Divisions)
                {
                    currentDivision = division;
                    page = 1;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var matches = await Task.WhenAll(
                            Regions.Keys.Select(platformRoute =>
                                FetchMatchDataList(platformRoute, tier, division, page, cancellationToken)
                            )
                        ).ConfigureAwait(false);

                        if (matches.All(m => m.Length == 0))
                            break;
                        
                        var matchesList = matches
                            .SelectMany(m => m)
                            .ToArray();
                        
                        var distinctSummoners = matchesList
                            .SelectMany(m => m!.Participants)
                            .Select(p => p.Summoner)
                            .DistinctBy(s => s.Puuid)
                            .ToDictionary(s => s.Puuid, s => s);

                        await context.BulkInsertOrUpdateAsync(distinctSummoners.Values, options =>
                        {
                            options.SetOutputIdentity = true;
                            options.UpdateByProperties = [nameof(Summoners.Puuid)];
                        }, cancellationToken: cancellationToken).ConfigureAwait(false);

                        foreach (var match in matchesList)
                        {
                            foreach (var participant in match.Participants)
                            {
                                participant.SummonersId = distinctSummoners[participant.Summoner.Puuid].Id;
                                participant.Summoner = null;
                            }
                        }

                        await context.BulkInsertOrUpdateAsync(matchesList, options =>
                        {
                            options.IncludeGraph = true;
                        }, cancellationToken: cancellationToken).ConfigureAwait(false);

                        Console.WriteLine("Imported {0} Matches for: {1} - {2} - Page {3} - {4}", matchesList.Length, tier, division, page, DateTime.Now);

                        totalMatches += matchesList.Length;
                        page += 1;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("An Error has Occurred. Please Check the Audit for More Information.");
            await context.AddAsync(
                new Audit()
                {
                    Method = nameof(BeginDataRoutine),
                    Input = JsonSerializer.Serialize(new { Tier = currentTier, Division = currentDivision, Page = page }),
                    Exception = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty
                }, cancellationToken).ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        
        Console.WriteLine("Imported {0} Matches in Total", totalMatches);
    }

    private static async Task<Matches[]> FetchMatchDataList(PlatformRoute platformRoute, Tier tier, Division division, int page, CancellationToken cancellationToken)
    {
        try
        {
            var leagueEntryList = await FetchLeagueEntriesAsync(platformRoute, tier, division, page, cancellationToken)
                .ConfigureAwait(false);
            
            if (leagueEntryList.Length == 0)
                return [];
                        
            var puuidList = await FetchPuuidListFromLeagueEntryList(platformRoute, leagueEntryList, cancellationToken)
                .ConfigureAwait(false);
                        
            var matchIdList = await FetchMatchIdListForPuuidList(platformRoute, Regions[platformRoute], puuidList, cancellationToken)
                .ConfigureAwait(false);
            
            var matchDataList = await Task.WhenAll(
                matchIdList
                    .Select(matchId => FetchMatchDataForMatchId(Regions[platformRoute], matchId, cancellationToken))
            ).ConfigureAwait(false);
            
            var matchesList = matchDataList
                .Select(ModelHelpers.ParseRiotMatchData)
                .ToArray();
            
            return matchesList;
        }
        catch (Exception e)
        {
            await using var context = CreateSeioriContext();
            
            Console.WriteLine("An Error has Occurred. Please Check the Audit for More Information.");
            await context.AddAsync(
                new Audit()
                {
                    Method = nameof(FetchMatchDataList),
                    Exception = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty
                }, cancellationToken).ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        
        return [];
    }
    
    private static async Task<IEnumerable<string>> FetchPuuidListFromLeagueEntryList(PlatformRoute platformRoute, LeagueEntry[] leagueEntryList, CancellationToken cancellationToken)
    {
        await using var context = CreateSeioriContext();
        
        try
        {
            var summonerIdList = leagueEntryList
                .Select(le => le.SummonerId)
                .ToHashSet();

            var existingSummonersDict = await context.Summoners
                .AsNoTracking()
                .Where(s => s.Platform == platformRoute && summonerIdList.Contains(s.SummonerId))
                .Select(s => new { s.SummonerId, s.Puuid })
                .ToDictionaryAsync(s => s.SummonerId, s => s.Puuid, cancellationToken).ConfigureAwait(false);

            var missingSummonerIdList = summonerIdList
                .Except(existingSummonersDict.Keys)
                .ToHashSet();

            var fetchedPuuidList = await Task.WhenAll(
                missingSummonerIdList
                    .Select(id => FetchPuuidForSummonerId(platformRoute, id, cancellationToken))
            ).ConfigureAwait(false);

            var puuidList = fetchedPuuidList
                .Where(puuid => !string.IsNullOrEmpty(puuid))
                .Concat(existingSummonersDict.Values)
                .ToHashSet();
            
            return puuidList;
        }
        catch (Exception e)
        {
            Console.WriteLine("An Error has Occurred. Please Check the Audit for More Information.");
            await context.AddAsync(
                new Audit()
                {
                    Method = nameof(FetchPuuidListFromLeagueEntryList),
                    Exception = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty
                }, cancellationToken).ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        
        return Array.Empty<string>();
    }
    
    private static async Task<IEnumerable<string>> FetchMatchIdListForPuuidList(PlatformRoute platformRoute, RegionalRoute regionalRoute, IEnumerable<string> puuidList, CancellationToken cancellationToken)
    {
        await using var context = CreateSeioriContext();

        try
        {
            var matchIdLists = await Task.WhenAll(
                puuidList
                    .Select(puuid => FetchMatchIdListForPuuid(regionalRoute, puuid!, cancellationToken))
            ).ConfigureAwait(false);

            var gameIdList = matchIdLists
                .SelectMany(matchIds => matchIds)
                .Select(id => long.Parse(id.Split('_')[1]))
                .ToHashSet();

            var existingGameIdList = await context.Matches
                .AsNoTracking()
                .Where(m => m.Platform == platformRoute && gameIdList
                    .Contains(m.GameId))
                .Select(m => m.GameId)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

            var newGameIdList = gameIdList
                .Except(existingGameIdList)
                .ToHashSet();
            
            var matchIdList = newGameIdList
                .Select(id => $"{platformRoute}_{id}")
                .ToHashSet();

            return matchIdList;
        }
        catch (Exception e)
        {
            Console.WriteLine("An Error has Occurred. Please Check the Audit for More Information.");
            await context.AddAsync(
                new Audit()
                {
                    Method = nameof(FetchMatchIdListForPuuidList),
                    Exception = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty
                }, cancellationToken).ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        
        return Array.Empty<string>();
    }

    private static async Task<LeagueEntry[]> FetchLeagueEntriesAsync(PlatformRoute platformRoute, Tier tier, Division division, int page, CancellationToken cancellationToken)
    {
        if (tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && division is not Division.I) 
            return [];

        return await _riotGamesApi!
                .LeagueExpV4()
                .GetLeagueEntriesAsync(
                    platformRoute, 
                    QueueType.RANKED_SOLO_5x5, 
                    tier, 
                    division, 
                    page, 
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false) 
               ?? [];
    }

    private static async Task<string> FetchPuuidForSummonerId(PlatformRoute platformRoute, string summonerId, CancellationToken cancellationToken)
    {
        return (await _riotGamesApi!
            .SummonerV4()
            .GetBySummonerIdAsync(
                platformRoute, 
                summonerId, 
                cancellationToken
            )
            .ConfigureAwait(false))
            .Puuid;
    }

    private static async Task<IEnumerable<string>> FetchMatchIdListForPuuid(RegionalRoute regionalRoute, string puuid, CancellationToken cancellationToken)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetMatchIdsByPUUIDAsync(
                regionalRoute,
                puuid,
                100,
                queue: Queue.SUMMONERS_RIFT_5V5_RANKED_SOLO,
                startTime: StartTime,
                endTime: EndTime,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<Match> FetchMatchDataForMatchId(RegionalRoute regionalRoute, string matchId, CancellationToken cancellationToken)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetMatchAsync(
                regionalRoute,
                matchId,
                cancellationToken
            )
            .ConfigureAwait(false)
            ?? new Match();
    }

    private static async Task<Timeline> FetchMatchTimelineForMatchId(RegionalRoute regionalRoute, string matchId, CancellationToken cancellationToken)
    {
        return await _riotGamesApi!
            .MatchV5()
            .GetTimelineAsync(
                regionalRoute,
                matchId,
                cancellationToken
            )
            .ConfigureAwait(false)
            ?? new Timeline();
    }
}
