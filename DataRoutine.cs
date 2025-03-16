using Camille.Enums;
using Camille.RiotGames;
using Camille.RiotGames.LeagueExpV4;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Helpers;
using Statikk_Scraper.Models;
using System.Threading.Tasks.Dataflow;

namespace Statikk_Scraper;

public class DataRoutine(IDbContextFactory<Context> contextFactory, RiotGamesApi riotGamesApi)
{
    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;

    private static readonly Dictionary<PlatformRoute, RegionalRoute> Regions = new()
    {
        [PlatformRoute.NA1] = RegionalRoute.AMERICAS,
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE,
        [PlatformRoute.KR] = RegionalRoute.ASIA
    };
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];

    public async Task BeginDataRoutine()
    {
        var processBlock = new ActionBlock<(PlatformRoute platform, Tier tier, Division division, int page, Dictionary<string, SummonerRanks> summonerRanks)>(
            async item =>
            {
                await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                await ProcessPage(context, item.platform, item.tier, item.division, item.page, item.summonerRanks).ConfigureAwait(false);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            }
        );
        
        var producerTasks = Regions.Keys.Select(async platform =>
        {
            var tierDivisionTasks = Tiers.SelectMany(_ => Divisions, (tier, division) => (tier, division))
                .Where(pair => !(pair.tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && pair.division != Division.I))
                .Select(async pair =>
                {
                    await foreach (var (page, summonerRanks) in GetSummonerRanksAsync(platform, pair.tier, pair.division).ConfigureAwait(false))
                    {
                        if (summonerRanks.Count == 0) break;
                        processBlock.Post((platform, pair.tier, pair.division, page, summonerRanks));
                    }
                });
            await Task.WhenAll(tierDivisionTasks);
        });
        await Task.WhenAll(producerTasks);
        
        processBlock.Complete();
        await processBlock.Completion;
        
        await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("EXECUTE SetRankedMatchAverages");
    }

    private async Task ProcessPage(Context context, PlatformRoute platform, Tier tier, Division division, int page, Dictionary<string, SummonerRanks> summonerRanks)
    {
        var matchIdList = await FetchMatchIdsFromPuuids(context, platform, Regions[platform], summonerRanks.Keys).ConfigureAwait(false);
        
        var matchesList = (await Task.WhenAll(matchIdList.Select(id => FetchMatchDataForMatchId(Regions[platform], id)))
                             .ConfigureAwait(false))
                             .OfType<Matches>()
                             .ToArray();
        if (matchesList.Length is 0) return;
        
        var distinctSummoners = matchesList
            .SelectMany(m => m.Participants)
            .Select(p => p.Summoner)
            .DistinctBy(s => s.Puuid)
            .ToDictionary(s => s.Puuid);

        try
        {
            // Bulk update/insert distinct summoners.
            await context.BulkInsertOrUpdateAsync(distinctSummoners.Values, options =>
            {
                options.SetOutputIdentity = true;
                options.UpdateByProperties = [nameof(Summoners.Puuid)];
            }).ConfigureAwait(false);
            
            var upToDateSummoners = await context.Summoners
                .Where(s => distinctSummoners.Keys.Contains(s.Puuid))
                .Select(s => new { s.Puuid, s.Id })
                .ToDictionaryAsync(s => s.Puuid);

            foreach (var key in upToDateSummoners.Keys)
            {
                if (distinctSummoners.TryGetValue(key, out var summoner))
                {
                    summoner.Id = upToDateSummoners[key].Id;
                }
            }
            
            if (distinctSummoners.Values.Any(s => s.Id == 0))
            {
                throw new Exception("Summoner has Id of 0");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error Inserting Summoners");
            Console.WriteLine("Error Message: " + e.Message);
            return;
        }
        
        summonerRanks = summonerRanks
            .Where(kvp => distinctSummoners.ContainsKey(kvp.Key))
            .ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    kvp.Value.SummonersId = distinctSummoners[kvp.Key].Id;
                    return kvp.Value;
                }
            );

        try
        {
            await context.BulkInsertOrUpdateAsync(summonerRanks.Values, options =>
                options.UpdateByProperties = [nameof(SummonerRanks.SummonersId), nameof(SummonerRanks.Queue)]
            ).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error Inserting Summoner Ranks");
            Console.WriteLine("Error Message: " + e.Message);
            return;
        }

        foreach (var match in matchesList)
        {
            foreach (var participant in match.Participants)
            {
                participant.SummonersId = distinctSummoners[participant.Summoner.Puuid].Id;
                participant.Summoner = null!;
            }
        }

        try
        {
            await context.BulkInsertAsync(matchesList, options => options.IncludeGraph = true).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error Inserting Matches");
            Console.WriteLine("Error Message: " + e.Message);
            return;
        }
        
        Console.WriteLine($"Processed {matchesList.Length} Matches and {summonerRanks.Count} Summoner Ranks for: {platform} - {tier} - {division} - Page {page}");
    }

    private async IAsyncEnumerable<(int Page, Dictionary<string, SummonerRanks> SummonerRanks)> GetSummonerRanksAsync(PlatformRoute platform, Tier tier, Division division)
    {
        for (var page = 1;; page++)
        {
            var leagueEntries = await FetchLeagueEntriesForPlatform(platform, tier, division, page);
            if (leagueEntries.Length == 0)
                yield break;

            var summonerRanks = leagueEntries.ToDictionary(
                le => le.Puuid,
                le => new SummonerRanks
                {
                    Queue = QueueType.RANKED_SOLO_5x5,
                    Tier = tier,
                    Division = division,
                    LeaguePoints = (short)le.LeaguePoints,
                    Wins = (short)le.Wins,
                    Losses = (short)le.Losses,
                    TotalGames = (short)(le.Wins + le.Losses)
                }
            );
            
            yield return (page, summonerRanks);
        }
    }
    
    private static async Task<T?> RetryAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (attempt == 3)
                {
                    return default;
                }
                Console.WriteLine($"Retrying attempt {attempt} of 3: {e.Message}");
                await Task.Delay(100 * attempt).ConfigureAwait(false);
            }
        }

        return default;
    }

    private async Task<IEnumerable<string>> FetchMatchIdsFromPuuids(Context context, PlatformRoute platform, RegionalRoute region, IEnumerable<string> puuidList)
    {
        var matchIdLists = await Task.WhenAll(puuidList.Select(puuid => FetchMatchIdListForPuuid(region, puuid)));
    
        var gameIds = matchIdLists
            .SelectMany(ids => ids)
            .Select(id => long.Parse(id[(id.IndexOf('_') + 1)..]))
            .ToHashSet();

        var existingGameIds = await context.Matches
            .AsNoTracking()
            .Where(m => m.Platform == platform && gameIds.Contains(m.GameId))
            .Select(m => m.GameId)
            .ToHashSetAsync()
            .ConfigureAwait(false);

        return gameIds.Except(existingGameIds)
            .Select(id => $"{platform}_{id}")
            .ToHashSet();
    }

    private async Task<LeagueEntry[]> FetchLeagueEntriesForPlatform(PlatformRoute platform, Tier tier, Division division, int page) => 
        await RetryAsync(() => riotGamesApi.LeagueExpV4().GetLeagueEntriesAsync(platform, QueueType.RANKED_SOLO_5x5, tier, division, page)).ConfigureAwait(false) ?? [];
    
    private async Task<IEnumerable<string>> FetchMatchIdListForPuuid(RegionalRoute region, string puuid) =>
        await RetryAsync(() => riotGamesApi.MatchV5().GetMatchIdsByPUUIDAsync(region, puuid, 100, startTime: StartTime, endTime: EndTime, queue: Queue.SUMMONERS_RIFT_5V5_RANKED_SOLO)).ConfigureAwait(false) ?? [];

    private async Task<Matches?> FetchMatchDataForMatchId(RegionalRoute region, string matchId) =>
        await RetryAsync(() => riotGamesApi.MatchV5().GetMatchAsync(region, matchId)).ConfigureAwait(false) is { } result ? ModelHelpers.ParseRiotMatchData(result) : null;
}