using System.Text.Json;
using Camille.Enums;
using Camille.RiotGames;
using Camille.RiotGames.LeagueExpV4;
using Camille.RiotGames.MatchV5;
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
        [PlatformRoute.EUW1] = RegionalRoute.EUROPE,
        [PlatformRoute.KR] = RegionalRoute.ASIA,
        [PlatformRoute.NA1] = RegionalRoute.AMERICAS,
    };

    private static readonly Tier[] Tiers =
    [
        Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER,
        Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM,
        Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON
    ];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];

    public async Task BeginDataRoutine()
    {
        var totalMatches = 0;
        await using var context = await contextFactory.CreateDbContextAsync();

        try
        {
            foreach (var tier in Tiers)
            {
                foreach (var division in Divisions)
                {
                    for (var page = 1; ; page++)
                    {
                        var matchArrays = await Task.WhenAll(
                            Regions.Keys.Select(platform => FetchMatchDataList(platform, tier, division, page))
                        ).ConfigureAwait(false);

                        if (matchArrays.All(m => m.Length == 0))
                            break;

                        var matchesList = matchArrays.SelectMany(m => m).ToArray();
                        var distinctSummoners = matchesList
                            .SelectMany(m => m.Participants)
                            .Select(p => p.Summoner)
                            .DistinctBy(s => s.Puuid)
                            .ToDictionary(s => s.Puuid);

                        await context.BulkInsertOrUpdateAsync(
                            distinctSummoners.Values,
                            options =>
                            {
                                options.SetOutputIdentity = true;
                                options.UpdateByProperties = [nameof(Summoners.Puuid)];
                            }).ConfigureAwait(false);

                        var summonerRanksList = distinctSummoners.Values
                            .Select(s =>
                            {
                                var rank = s.Ranks.FirstOrDefault();
                                if (rank != null) rank.SummonersId = s.Id;
                                return rank;
                            })
                            .Where(r => r != null)
                            .ToArray();

                        await context.BulkInsertOrUpdateAsync(
                            summonerRanksList!,
                            options => options.UpdateByProperties = [nameof(SummonerRanks.SummonersId), nameof(SummonerRanks.Queue)]
                        ).ConfigureAwait(false);

                        Parallel.ForEach(matchesList, match =>
                        {
                            var participantRanks = new List<SummonerRanks>();
                            foreach (var participant in match.Participants)
                            {
                                var puuid = participant.Summoner.Puuid;
                                var summoner = distinctSummoners[puuid];
                                participant.SummonersId = summoner.Id;
        
                                // Retrieve the rank (if any) and add to our list for averaging.
                                var rank = summoner.Ranks.FirstOrDefault();
                                if (rank != null)
                                {
                                    participantRanks.Add(rank);
                                }
        
                                // Clear the Summoner reference.
                                participant.Summoner = null;
                            }
    
                            // Compute and assign the average Tier and Division if any ranks exist.
                            if (participantRanks.Count == 0) return;
                            match.Tier = (Tier)Math.Round(participantRanks.Average(r => (int)r.Tier));
                            match.Division = (Division)Math.Round(participantRanks.Average(r => (int)r.Division));
                            match.LeaguePoints = (short)Math.Round(participantRanks.Average(r => r.LeaguePoints));
                        });

                        await context.BulkInsertOrUpdateAsync(
                            matchesList,
                            options => options.IncludeGraph = true
                        ).ConfigureAwait(false);

                        Console.WriteLine($"Imported {matchesList.Length} Matches for: {tier} - {division} - Page {page} - {DateTime.Now}");
                        totalMatches += matchesList.Length;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("An error has occurred. Please check the Audit for more information.");
            context.ChangeTracker.Clear();

            var audit = new Audit
            {
                Method = nameof(BeginDataRoutine),
                Input = JsonSerializer.Serialize(new { e.Message }),
                Exception = e.Message,
                StackTrace = e.StackTrace ?? string.Empty
            };

            await context.AddAsync(audit).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        Console.WriteLine($"Imported {totalMatches} Matches in Total");
    }

    private async Task<Matches[]> FetchMatchDataList(PlatformRoute platform, Tier tier, Division division, int page)
    {
        await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var leagueEntries = await FetchLeagueEntriesAsync(platform, tier, division, page).ConfigureAwait(false);
        if (leagueEntries.Length == 0)
            return [];

        var leagueEntriesBySummoner = leagueEntries.ToDictionary(le => le.SummonerId);
        var region = Regions[platform];
        var puuids = await FetchPuuidListFromLeagueEntryList(context, platform, leagueEntriesBySummoner.Keys).ConfigureAwait(false);
        var matchIds = await FetchMatchIdListForPuuidList(context, platform, region, puuids).ConfigureAwait(false);

        var matchDataList = await Task.WhenAll(matchIds.Select(id => FetchMatchDataForMatchId(region, id)))
                                       .ConfigureAwait(false);
        var matchesList = matchDataList
            .Where(m => m is not null)
            .Select(ModelHelpers.ParseRiotMatchData!)
            .ToArray();

        foreach (var match in matchesList)
        {
            foreach (var participant in match.Participants)
            {
                if (leagueEntriesBySummoner.TryGetValue(participant.Summoner.SummonerId, out var leagueEntry))
                {
                    participant.Summoner.Ranks.Add(new SummonerRanks
                    {
                        Queue = QueueType.RANKED_SOLO_5x5,
                        Tier = tier,
                        Division = division,
                        LeaguePoints = (short)leagueEntry.LeaguePoints,
                        Wins = (short)leagueEntry.Wins,
                        Losses = (short)leagueEntry.Losses,
                        TotalGames = (short)(leagueEntry.Wins + leagueEntry.Losses),
                    });
                }
            }
        }
        return matchesList;
    }

    private async Task<IEnumerable<string>> FetchPuuidListFromLeagueEntryList(Context context, PlatformRoute platform, IEnumerable<string> summonerIds)
    {
        var existingSummoners = await context.Summoners
            .AsNoTracking()
            .Where(s => s.Platform == platform && summonerIds.Contains(s.SummonerId))
            .Select(s => new { s.SummonerId, s.Puuid })
            .ToArrayAsync().ConfigureAwait(false);

        var existingDict = existingSummoners.ToDictionary(s => s.SummonerId, s => s.Puuid);
        var missingIds = summonerIds.Except(existingDict.Keys);
        var fetchedPuuids = (await Task.WhenAll(missingIds.Select(id => FetchPuuidForSummonerId(platform, id))).ConfigureAwait(false))
            .Where(puuid => !string.IsNullOrEmpty(puuid));
        return existingDict.Values.Concat(fetchedPuuids);
    }

    private async Task<IEnumerable<string>> FetchMatchIdListForPuuidList(Context context, PlatformRoute platform, RegionalRoute region, IEnumerable<string> puuids)
    {
        var matchIdArrays = await Task.WhenAll(puuids.Select(puuid => FetchMatchIdListForPuuid(region, puuid))).ConfigureAwait(false);
        var gameIds = matchIdArrays.SelectMany(ids => ids)
                                   .Select(id => long.Parse(id[(id.IndexOf('_') + 1)..]))
                                   .ToHashSet();

        var existingGameIds = await context.Matches
            .AsNoTracking()
            .Where(m => m.Platform == platform && gameIds.Contains(m.GameId))
            .Select(m => m.GameId)
            .ToHashSetAsync().ConfigureAwait(false);

        var newGameIds = gameIds.Except(existingGameIds);
        return newGameIds.Select(id => $"{platform}_{id}").ToHashSet();
    }

    private async Task<LeagueEntry[]> FetchLeagueEntriesAsync(PlatformRoute platform, Tier tier, Division division, int page)
    {
        if (tier is Tier.CHALLENGER or Tier.GRANDMASTER or Tier.MASTER && division != Division.I)
            return [];

        return await riotGamesApi.LeagueExpV4()
            .GetLeagueEntriesAsync(platform, QueueType.RANKED_SOLO_5x5, tier, division, page)
            .ConfigureAwait(false) ?? [];
    }

    private Task<string> FetchPuuidForSummonerId(PlatformRoute platform, string summonerId) =>
        riotGamesApi.SummonerV4().GetBySummonerIdAsync(platform, summonerId)
            .ContinueWith(t => t.Result.Puuid);

    private async Task<IEnumerable<string>> FetchMatchIdListForPuuid(RegionalRoute region, string puuid) =>
        await riotGamesApi.MatchV5()
            .GetMatchIdsByPUUIDAsync(region, puuid, 100, startTime: StartTime, endTime: EndTime)
            .ConfigureAwait(false);

    private async Task<Match?> FetchMatchDataForMatchId(RegionalRoute region, string matchId) =>
        await riotGamesApi.MatchV5().GetMatchAsync(region, matchId).ConfigureAwait(false);

    private async Task<Timeline> FetchMatchTimelineForMatchId(RegionalRoute region, string matchId) =>
        await riotGamesApi.MatchV5().GetTimelineAsync(region, matchId)
            .ConfigureAwait(false) ?? new Timeline();
}