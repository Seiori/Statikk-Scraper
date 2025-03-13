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

namespace Statikk_Scraper;

public class DataRoutine(IDbContextFactory<Context> contextFactory, RiotGamesApi riotGamesApi)
{
    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;

    private static readonly Dictionary<PlatformRoute, RegionalRoute> Regions = new()
    {
        [PlatformRoute.NA1] = RegionalRoute.AMERICAS,
        [PlatformRoute.KR] = RegionalRoute.ASIA,
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE
    };
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];
    
    public async Task BeginDataRoutine()
    {
        try
        {
            await Task.WhenAll(
                Regions.Keys.Select(async platform =>
                {
                    var tierDivisionTasks = Tiers.SelectMany(_ => Divisions, (tier, division) => (tier, division))
                        .Where(pair => !(pair.tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && pair.division != Division.I))
                        .Select(async pair =>
                        {
                            await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                            await foreach (var (page, summonerRanks) in GetSummonerRanksAsync(platform, pair.tier, pair.division).ConfigureAwait(false))
                            {
                                if (summonerRanks.Count is 0) break;
                                var matchIdList = await FetchMatchIdsFromPuuids(context, platform, Regions[platform], summonerRanks.Keys).ConfigureAwait(false);
                                var matchesList = (await Task.WhenAll(matchIdList.Select(id => FetchMatchDataForMatchId(Regions[platform], id))).ConfigureAwait(false)).OfType<Matches>().ToArray();
                                if (matchesList.Length is 0) continue;
                                
                                var distinctSummoners = matchesList
                                    .SelectMany(m => m.Participants)
                                    .Select(p => p.Summoner)
                                    .DistinctBy(s => s.Puuid)
                                    .ToDictionary(s => s.Puuid);
                                
                                await context.BulkInsertOrUpdateAsync(distinctSummoners.Values, options =>
                                {
                                    options.SetOutputIdentity = true;
                                    options.UpdateByProperties = [nameof(Summoners.Puuid)];
                                }).ConfigureAwait(false);

                                foreach (var key in summonerRanks.Keys.ToArray())
                                {
                                    if (distinctSummoners.TryGetValue(key, out var summoner))
                                    {
                                        summonerRanks[key].SummonersId = summoner.Id;
                                    }
                                    else
                                    {
                                        summonerRanks.Remove(key, out _);
                                    }
                                }

                                await context.BulkInsertOrUpdateAsync(summonerRanks.Values, options =>
                                    options.UpdateByProperties = [nameof(SummonerRanks.SummonersId), nameof(SummonerRanks.Queue)]
                                ).ConfigureAwait(false);

                                Parallel.ForEach(matchesList, match =>
                                {
                                    foreach (var participant in match.Participants)
                                    {
                                        participant.Summoner = null!;
                                    }
                                });
                                
                                await context.BulkInsertAsync(matchesList, options => options.IncludeGraph = true).ConfigureAwait(false);
                                
                                Console.WriteLine($"Processed {matchesList.Length} Matches and {summonerRanks.Count} Summoner Ranks for: {platform} - {pair.tier} - {pair.division} - Page {page}");
                            }
                        });
                    await Task.WhenAll(tierDivisionTasks);
                    return Task.CompletedTask;
                })
            );
        }
        catch (Exception e)
        {
            Console.WriteLine("Message: " + e.Message);
            Console.WriteLine("Stack Trace: " + e.StackTrace);
        }
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
    
    private async Task<T?> RetryAsync<T>(Func<Task<T>> operation)
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