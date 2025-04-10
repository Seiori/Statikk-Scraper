using Camille.Enums;
using Camille.RiotGames;
using Camille.RiotGames.LeagueExpV4;
using Microsoft.EntityFrameworkCore;
using Statikk_Scraper.Data;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Helpers;
using Statikk_Scraper.Models;
using System.Threading.Tasks.Dataflow;
using Seiori.MySql;
using Seiori.MySql.Enums;
using Sta.Data.Models;
using Queue = Camille.Enums.Queue;

namespace Statikk_Scraper;

public class DataRoutine(IDbContextFactory<Context> contextFactory, RiotGamesApi riotGamesApi)
{
    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;
    private static Dictionary<string, ushort> _patchList = new();

    private static readonly Dictionary<PlatformRoute, RegionalRoute> Regions = new()
    {
        [PlatformRoute.NA1] = RegionalRoute.AMERICAS,
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE,
        [PlatformRoute.KR] = RegionalRoute.ASIA,
    };
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];
    
    public async Task BeginDataRoutine()
    {
        _patchList = await GetPatchesAsync();

        var platformTasks = Regions.Keys.Select(async platform =>
        {
            var tierDivisionPairs = Tiers
                .SelectMany(tier => Divisions, (tier, division) => (tier, division))
                .ToArray();

            foreach (var (tier, division) in tierDivisionPairs)
            {
                await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                
                await foreach (var (page, summonerRanks) in GetSummonerRanksAsync(platform, tier, division).ConfigureAwait(false))
                {
                    if (summonerRanks.Count is 0) break;
                    
                    await ProcessPage(context, platform, tier, division, page, summonerRanks).ConfigureAwait(false);
                }
            }
        });
        
        await Task.WhenAll(platformTasks).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, ushort>> GetPatchesAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        
        return await context.Patches
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Take(2)
            .Select(p => new { p.Id, p.PatchVersion })
            .ToDictionaryAsync(p => p.PatchVersion, p => p.Id)
            .ConfigureAwait(false);
    }
    
    private async Task ProcessPage(Context context, PlatformRoute platform, Tier tier, Division division, int page, Dictionary<string, SummonerRanks> summonerRanks)
    {
        try
        {
            var matchIdList = await FetchMatchIdsFromPuuidList(context, platform, Regions[platform], summonerRanks.Keys).ConfigureAwait(false);
            
            var matchesList = (
                    await Task.WhenAll(
                            matchIdList.Select(id => FetchMatchDataForMatchId(Regions[platform], id)
                            )
                    ).ConfigureAwait(false)
            ).OfType<Matches>().ToArray();
            
            if (matchesList.Length is 0) return;
            
            var distinctSummoners = matchesList
                .SelectMany(m => m.Participants)
                .Select(p => p.Summoner)
                .DistinctBy(s => s.Puuid)
                .ToDictionary(s => s.Puuid);
            
            await context.BulkOperationAsync(BulkOperation.Upsert, distinctSummoners.Values, options => options.SetOutputIdentity = true).ConfigureAwait(false);
            
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

            await context.BulkOperationAsync(BulkOperation.Upsert, summonerRanks.Values, options => {}).ConfigureAwait(false);

            var filteredMatchesList = new List<Matches>();
            foreach (var match in matchesList)
            {
                if (match.Patch?.PatchVersion is not null && _patchList.TryGetValue(match.Patch.PatchVersion, out var patchId))
                {
                    match.PatchesId = patchId;
                }
                else
                {
                    continue;
                }
                
                foreach (var participant in match.Participants)
                {
                    participant.SummonersId = distinctSummoners[participant.Summoner.Puuid].Id;
                }
                
                filteredMatchesList.Add(match);
            }
            
            if (filteredMatchesList.Count is 0) return;

            await context.BulkOperationAsync(BulkOperation.Insert, filteredMatchesList, options =>
            {
                options.SetOutputIdentity = true;
                options.IncludeChildEntities = true;
                options.ExcludedNavigationPropertyNames = [nameof(Champions), nameof(Patches), nameof(Summoners)];
            }).ConfigureAwait(false);
            
            Console.WriteLine($"Processed {filteredMatchesList.Count} Matches, {summonerRanks.Count} Summoner Ranks, {distinctSummoners.Count} Summoners for: {platform} - {tier} - {division} - Page {page}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error processing page {page} for {platform} - {tier} - {division}: {e.Message}");
            Console.WriteLine(e.StackTrace);
            throw;
        }
    }
    
    private async IAsyncEnumerable<(int Page, Dictionary<string, SummonerRanks> SummonerRanks)> GetSummonerRanksAsync(PlatformRoute platform, Tier tier, Division division)
    {
        for (var page = 1;; page++)
        {
            var leagueEntries = await FetchLeagueEntriesForPlatform(platform, tier, division, page);
            if (leagueEntries.Length is 0)
                yield break;

            var summonerRanks = leagueEntries.ToDictionary(
                le => le.Puuid,
                le => new SummonerRanks
                {
                    Queue = QueueType.RANKED_SOLO_5x5,
                    Tier = tier,
                    Division = division,
                    LeaguePoints = (ushort)le.LeaguePoints,
                    Wins = (ushort)le.Wins,
                    Losses = (ushort)le.Losses,
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
                if (attempt is 3)
                {
                    return default;
                }
                Console.WriteLine($"Retrying attempt {attempt} of 3: {e.Message}");
                await Task.Delay(100 * attempt).ConfigureAwait(false);
            }
        }

        return default;
    }

    private async Task<IEnumerable<string>> FetchMatchIdsFromPuuidList(Context context, PlatformRoute platform, RegionalRoute region, IEnumerable<string> puuidList)
    {
        var matchIdLists = await Task.WhenAll(puuidList.Select(puuid => FetchMatchIdListForPuuid(region, puuid)));
    
        var gameIds = matchIdLists
            .SelectMany(ids => ids)
            .Select(id => ulong.Parse(id[(id.IndexOf('_') + 1)..]))
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