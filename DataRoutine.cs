using System.Collections.Concurrent;
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
            var summonerRanksDict = new ConcurrentDictionary<string, SummonerRanks>();
            var matchesDict = new ConcurrentDictionary<string, Matches>();
            
            await Task.WhenAll(
                Regions.Keys.Select(async platform =>
                {
                    var tierDivisionTasks = Tiers.SelectMany(_ => Divisions, (tier, division) => (tier, division))
                        .Where(pair => !(pair.tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && pair.division != Division.I))
                        .Select(async pair =>
                        {
                            await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                            var page = 1;
                            await foreach (var leagueEntries in GetLeagueEntriesAsync(platform, pair.tier, pair.division))
                            {
                                var summonerRanks = leagueEntries.ToDictionary(
                                    le => le.Puuid,
                                    le => new SummonerRanks
                                    {
                                        Queue = QueueType.RANKED_SOLO_5x5,
                                        Tier = pair.tier,
                                        Division = pair.division,
                                        LeaguePoints = (short)le.LeaguePoints,
                                        Wins = (short)le.Wins,
                                        Losses = (short)le.Losses,
                                        TotalGames = (short)(le.Wins + le.Losses)
                                    }
                                );

                                foreach (var kvp in summonerRanks)
                                {
                                    summonerRanksDict.AddOrUpdate(kvp.Key, kvp.Value, (_, _) => kvp.Value);
                                }

                                var matchIdList = await FetchMatchIdsFromPuuids(context, platform, Regions[platform], summonerRanks.Keys);
                                matchIdList = matchIdList.Except(matchesDict.Keys);
                                var matchDataList = await Task.WhenAll(matchIdList.Select(id => FetchMatchDataForMatchId(Regions[platform], id)));
                                matchDataList.OfType<Matches>().ToList().ForEach(match =>
                                    matchesDict.TryAdd($"{match.Platform}_{match.GameId}", match)
                                );
                                
                                Console.WriteLine($"Processed {matchDataList.Length} Matches and {summonerRanks.Count} Summoner Ranks for: {platform} - {pair.tier} - {pair.division} - Page {page} at {DateTime.UtcNow}");
                                page++;
                            }
                        });
                    await Task.WhenAll(tierDivisionTasks);
                    return Task.CompletedTask;
                })
            );
            
            await using var finalContext = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                
            var distinctSummoners = matchesDict.Values
                .SelectMany(m => m.Participants)
                .Select(p => p.Summoner)
                .DistinctBy(s => s.Puuid)
                .ToDictionary(s => s.Puuid);
            
            await finalContext.BulkInsertOrUpdateAsync(distinctSummoners.Values, options =>
            {
                options.SetOutputIdentity = true;
                options.UpdateByProperties = [nameof(Summoners.Puuid)];
            }).ConfigureAwait(false);

            foreach (var key in summonerRanksDict.Keys)
            {
                if (distinctSummoners.TryGetValue(key, out var summoner))
                {
                    summonerRanksDict[key].SummonersId = summoner.Id;
                }
                else
                {
                    summonerRanksDict.TryRemove(key, out _);
                }
            }

            await finalContext.BulkInsertOrUpdateAsync(summonerRanksDict.Values, options =>
                options.UpdateByProperties = [nameof(SummonerRanks.SummonersId), nameof(SummonerRanks.Queue)]
            ).ConfigureAwait(false);

            Parallel.ForEach(matchesDict.Values, match =>
            {
                var participantRanks = match.Participants
                    .Select(p => summonerRanksDict.GetValueOrDefault(p.Summoner.Puuid))
                    .Where(rank => rank != null)
                    .ToArray();

                if (participantRanks.Length == 0)
                    return;

                match.Tier = (Tier)Math.Round(participantRanks.Average(r => (int)r!.Tier));
                match.Division = (Division)Math.Round(participantRanks.Average(r => (int)r!.Division));
                match.LeaguePoints = (short)Math.Round(participantRanks.Average(r => r!.LeaguePoints));

                foreach (var participant in match.Participants)
                {
                    participant.Summoner = null!;
                }
            });
            
            await finalContext.BulkInsertAsync(matchesDict.Values, options => options.IncludeGraph = true).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine("Message: " + e.Message);
            Console.WriteLine("Stack Trace: " + e.StackTrace);
        }
    }
    
    private async IAsyncEnumerable<LeagueEntry[]> GetLeagueEntriesAsync(PlatformRoute platform, Tier tier, Division division)
    {
        for (var page = 1;; page++)
        {
            var leagueEntries = await FetchLeagueEntriesForPlatform(platform, tier, division, page);
            if (leagueEntries.Length == 0)
                yield break;

            yield return leagueEntries;
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
                Console.WriteLine($"Retrying attempt {attempt} of 3: {e.Message}");
                if (attempt == 3)
                {
                    return default;
                }
                await Task.Delay(1000 * attempt).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("Unreachable code in RetryAsync.");
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
            .ToHashSetAsync();

        return gameIds.Except(existingGameIds)
            .Select(id => $"{platform}_{id}")
            .ToHashSet();
    }

    private async Task<LeagueEntry[]> FetchLeagueEntriesForPlatform(PlatformRoute platform, Tier tier, Division division, int page) => 
        await RetryAsync(() => riotGamesApi.LeagueExpV4().GetLeagueEntriesAsync(platform, QueueType.RANKED_SOLO_5x5, tier, division, page)) ?? [];
    
    private async Task<IEnumerable<string>> FetchMatchIdListForPuuid(RegionalRoute region, string puuid) =>
        await RetryAsync(() => riotGamesApi.MatchV5().GetMatchIdsByPUUIDAsync(region, puuid, 100, startTime: StartTime, endTime: EndTime, queue: Queue.SUMMONERS_RIFT_5V5_RANKED_SOLO)) ?? [];

    private async Task<Matches?> FetchMatchDataForMatchId(RegionalRoute region, string matchId) =>
        (await RetryAsync(() => riotGamesApi.MatchV5().GetMatchAsync(region, matchId))) is { } result
            ? ModelHelpers.ParseRiotMatchData(result) : null;
}