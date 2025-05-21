using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Seiori.MySql;
using Seiori.MySql.Enums;
using Statikk_Scraper.Data;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;
using Seiori.RiotAPI;
using Seiori.RiotAPI.Classes.Match_V5;
using Seiori.RiotAPI.Enums;
using Sta.Data.Models;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper;

public class ImportRoutine(IDbContextFactory<Context> contextFactory, RiotApiClient riotGamesApi)
{
    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;
    private static Dictionary<string, Patches> _patchList = new();

    private static readonly Dictionary<Region, RegionalRoute> Regions = new()
    {
        [Region.EUW1] = RegionalRoute.Europe,
        [Region.NA1] = RegionalRoute.Americas,
        [Region.KR] = RegionalRoute.Asia,
        [Region.OC1] = RegionalRoute.Sea
    };
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];

    public async Task BeginImportRoutine()
    {
        _patchList = await GetPatchesAsync();

        var regionalRouteTasks = Regions.Keys.Select(async regionalRoute =>
        {
            await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            
            try
            {
                var summonerRanksConcurrentDict = new ConcurrentDictionary<string, SummonerRanks>();

                await Task.WhenAll(Tiers.Select(async tier =>
                    {
                        await Task.WhenAll(Divisions.Select(async division =>
                            {
                                var page = 1;

                                while (true)
                                {
                                    var leagueEntries = await riotGamesApi.GetLeagueEntriesAsync(regionalRoute, Queue.RANKED_SOLO_5x5, tier, division, page);
                                    if (leagueEntries.Length is 0) break;

                                    foreach (var leagueEntry in leagueEntries)
                                    {
                                        summonerRanksConcurrentDict.TryAdd(leagueEntry.Puuid, new SummonerRanks()
                                        {
                                            Season = _patchList.First().Value.Season,
                                            Queue = Queue.RANKED_SOLO_5x5,
                                            Date = DateOnly.FromDateTime(DateTime.UtcNow),
                                            Tier = tier,
                                            Division = division,
                                            LeaguePoints = (ushort)leagueEntry.LeaguePoints,
                                            Wins = (ushort)leagueEntry.Wins,
                                            Losses = (ushort)leagueEntry.Losses,
                                        });
                                    }

                                    Console.WriteLine($"Processed League Entries for {tier} {division} on page {page} for {regionalRoute}");
                                    page++;
                                }
                            }
                        ));
                    })
                );
                
                var summonerRanksDict = summonerRanksConcurrentDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                summonerRanksConcurrentDict.Clear();

                var matchesList = new List<Matches>();
                var matchesListCount = 0;
                
                foreach (var summonerRanksKvpBatch in summonerRanksDict.Chunk(100))
                {
                    var matchIdLists = await Task.WhenAll(
                        summonerRanksKvpBatch.Select(sr =>
                            riotGamesApi.GetMatchIdsForPuuidAsync(Regions[regionalRoute], sr.Key, StartTime, EndTime, 420, 0, 100))
                    ).ConfigureAwait(false);

                    var matchIdList = matchIdLists.SelectMany(midl => midl);

                    var matchList = await Task.WhenAll(
                        matchIdList.Select(mid =>
                            riotGamesApi.GetMatchAsync(Regions[regionalRoute], mid))
                    ).ConfigureAwait(false);

                    var validMatchList = matchList.OfType<Match>().ToArray();

                    matchesList.AddRange(validMatchList.Select(ParseRiotMatchData));

                    if (matchesList.Count < 2000) continue;
                    
                    await ProcessMatches(context, regionalRoute, matchesList, summonerRanksDict).ConfigureAwait(false);
                    
                    matchesListCount += matchesList.Count;
                    matchesList.Clear();
                }
                
                await ProcessMatches(context, regionalRoute, matchesList, summonerRanksDict).ConfigureAwait(false);
                
                foreach (var summonerRank in summonerRanksDict.Where(summonerRank => summonerRank.Value.SummonersId is 0))
                {
                    summonerRanksDict.Remove(summonerRank.Key);
                }
                
                await context.BulkOperationAsync(BulkOperation.Upsert, summonerRanksDict.Values);
                
                Console.WriteLine($"Processed {matchesListCount} Matches for {regionalRoute}");
                Console.WriteLine($"Processed {summonerRanksDict.Count} Summoner Ranks for {regionalRoute}");
            }
            catch (Exception e)
            {
                await context.Audits.AddAsync(new Audits()
                {
                    Method = nameof(BeginImportRoutine),
                    Input = JsonSerializer.Serialize(regionalRoute),
                    Message = e.Message,
                    StackTrace = e.StackTrace ?? string.Empty,
                    Status = Status.ERROR,
                    IPAddress = IPAddress.None,
                });
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        });
        
        await Task.WhenAll(regionalRouteTasks).ConfigureAwait(false);
    }

    private static async Task ProcessMatches(Context context, Region region, List<Matches> matchesList, Dictionary<string, SummonerRanks> summonerRanksDict)
    {
        foreach (var match in matchesList)
        {
            var teams = match.Teams;
            var participants = teams.SelectMany(t => t.Participants).ToArray();
            var summoners = participants.Select(p => p.Summoner).ToArray();

            var averageTier = 0;
            var averageDivision = 0;
            var averageLeaguePoints = 0;

            foreach (var summoner in summoners)
            {
                var summonerRank = summonerRanksDict.GetValueOrDefault(summoner.Puuid);
                
                if (summonerRank is null) continue;
                
                averageTier += (int)summonerRank.Tier;
                averageDivision += (int)summonerRank.Division;
                averageLeaguePoints += summonerRank.LeaguePoints;
            }
            
            var roundedTierValue = (int)Math.Round((double)averageTier / summoners.Length);
            match.Tier = Enum.IsDefined(typeof(Tier), roundedTierValue)
                ? (Tier)roundedTierValue
                : Tier.NONE;

            var roundedDivisionValue = (int)Math.Round((double)averageDivision / summoners.Length);
            match.Division = Enum.IsDefined(typeof(Division), roundedDivisionValue)
                ? (Division)roundedDivisionValue
                : Division.NONE;
            
            match.LeaguePoints = (ushort)Math.Max(
                ushort.MinValue,
                Math.Min(ushort.MaxValue, Math.Round(
                    (double)averageLeaguePoints / summoners.Length
                ))
            );
        }
        
        var teamsList = matchesList.Select(matches => matches.Teams).SelectMany(teams => teams).ToArray();
        var participantsList = teamsList.Select(teams => teams.Participants).SelectMany(participants => participants).ToArray();
        var summonersList = participantsList.Select(participants => participants.Summoner).ToArray();

        await context.BulkOperationAsync(BulkOperation.Upsert, summonersList, options => options.SetOutputIdentity = true);
                
        foreach (var participant in participantsList)
        {
            participant.SummonersId = participant.Summoner.Id;
        }
                
        await context.BulkOperationAsync(BulkOperation.Insert, matchesList, options => options.SetOutputIdentity = true);
        await context.BulkOperationAsync(BulkOperation.Insert, teamsList, options => options.SetOutputIdentity = true);
        await context.BulkOperationAsync(BulkOperation.Insert, participantsList, options => options.SetOutputIdentity = true);
                
        Console.WriteLine($"Processed {matchesList.Count} Matches for {region}");
        
        foreach (var summoner in summonersList)
        {
            if (summonerRanksDict.TryGetValue(summoner.Puuid, out var summonerRank))
            {
                summonerRank.SummonersId = summoner.Id;
            }
        }
    }
    
    private async Task<Dictionary<string, Patches>> GetPatchesAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        return await context.Patches
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Take(2)
            .ToDictionaryAsync(p => p.PatchVersion, p => p)
            .ConfigureAwait(false);
    }
    
    private static Matches ParseRiotMatchData(Match matchData)
    {
        var matchInfo = matchData.Info;
        var region = Enum.Parse<Region>(matchInfo.PlatformId);
        var versionParts = matchInfo.GameVersion.Split('.');
        var patchVersion = $"{versionParts[0]}.{versionParts[1]}";
        
        var match = new Matches
        {
            GameId       = (ulong)matchInfo.GameId,
            Region       = region,
            PatchesId    = _patchList[patchVersion].Id,
            Queue        = (ushort)matchInfo.QueueId,
            DatePlayed   = DateTimeOffset.FromUnixTimeMilliseconds(matchInfo.GameCreation).UtcDateTime,
            TimePlayed   = TimeSpan.FromSeconds(matchInfo.GameDuration),
            EndOfGameResult       = matchInfo.EndOfGameResult
        };

        foreach (var matchTeam in matchInfo.Teams)
        {
            var feats = matchTeam.Feats;
            var objectives = matchTeam.Objectives;

            var team = new Teams
            {
                TeamId = (ushort)matchTeam.TeamId,

                Ban1 = (ushort)matchTeam.Bans[0].ChampionId <= 0 ? (ushort)0 : (ushort)matchTeam.Bans[0].ChampionId,
                Ban2 = (ushort)matchTeam.Bans[1].ChampionId <= 0 ? (ushort)0 : (ushort)matchTeam.Bans[1].ChampionId,
                Ban3 = (ushort)matchTeam.Bans[2].ChampionId <= 0 ? (ushort)0 : (ushort)matchTeam.Bans[2].ChampionId,
                Ban4 = (ushort)matchTeam.Bans[3].ChampionId <= 0 ? (ushort)0 : (ushort)matchTeam.Bans[3].ChampionId,
                Ban5 = (ushort)matchTeam.Bans[4].ChampionId <= 0 ? (ushort)0 : (ushort)matchTeam.Bans[4].ChampionId,
                
                EpicMonsterKill = (ushort)feats.EpicMonsterKill.FeatState,
                FirstBlood      = (ushort)feats.FirstBlood.FeatState,
                FirstTurret     = (ushort)feats.FirstTurret.FeatState,

                BaronKills      = (byte)objectives.Baron.Kills,
                ChampionKills   = (byte)objectives.Champion.Kills,
                DragonKills     = (byte)objectives.Dragon.Kills,
                HordeKills      = (byte)objectives.Horde.Kills,
                InhibitorKills  = (byte)objectives.Inhibitor.Kills,
                RiftHeraldKills = (byte)objectives.RiftHerald.Kills,
                TowerKills      = (byte)objectives.Tower.Kills,
            };

            foreach (var matchParticipant in matchInfo.Participants.Where(p => p.TeamId == matchTeam.TeamId))
            {
                var perks = matchParticipant.Perks;
                var primaryStyle = perks.Styles.First();
                var secondaryStyle = perks.Styles.Last();

                var summoner = new Summoners
                {
                    Puuid         = matchParticipant.Puuid,
                    SummonerId    = matchParticipant.SummonerId,
                    Region        = region,
                    RiotId        = NormalizeRiotId($"{matchParticipant.RiotIdGameName}#{matchParticipant.RiotIdTagline}"),
                    ProfileIconId = (ushort)matchParticipant.ProfileIcon,
                    SummonerLevel = (ushort)matchParticipant.SummonerLevel
                };
                
                
                team.Participants.Add(new Participants
                {
                    Role = ConvertRole(matchParticipant.TeamPosition),
                    ChampionsId = (ushort)matchParticipant.ChampionId,
                    ChampionLevel = (byte)matchParticipant.ChampLevel,
                    PrimaryPageId = (ushort)primaryStyle.Style,
                    PrimaryPageKeystoneId = (ushort)primaryStyle.Selections.First().Perk,
                    PrimaryPageRow1Id = (ushort)primaryStyle.Selections.Skip(1).First().Perk,
                    PrimaryPageRow2Id = (ushort)primaryStyle.Selections.Skip(2).First().Perk,
                    PrimaryPageRow3Id = (ushort)primaryStyle.Selections.Skip(3).First().Perk,
                    SecondaryPageId = (ushort)secondaryStyle.Style,
                    SecondaryPageOption1Id = (ushort)secondaryStyle.Selections.First().Perk,
                    SecondaryPageOption2Id = (ushort)secondaryStyle.Selections.Skip(1).First().Perk,
                    OffensiveStatId = (ushort)perks.PerkStats.Offense,
                    DefensiveStatId = (ushort)perks.PerkStats.Defense,
                    FlexStatId = (ushort)perks.PerkStats.Flex,
                    SummonerSpell1Id = (ushort)matchParticipant.Summoner1Id,
                    SummonerSpell2Id = (ushort)matchParticipant.Summoner2Id,
                    Item1Id = (ushort)matchParticipant.Item0,
                    Item2Id = (ushort)matchParticipant.Item1,
                    Item3Id = (ushort)matchParticipant.Item2,
                    Item4Id = (ushort)matchParticipant.Item3,
                    Item5Id = (ushort)matchParticipant.Item4,
                    Item6Id = (ushort)matchParticipant.Item5,
                    Item7Id = (ushort)matchParticipant.Item6,
                    TotalQCasts = (ushort)matchParticipant.Spell1Casts,
                    TotalWCasts = (ushort)matchParticipant.Spell2Casts,
                    TotalECasts = (ushort)matchParticipant.Spell3Casts,
                    TotalRCasts = (ushort)matchParticipant.Spell4Casts,
                    Kills = (byte)matchParticipant.Kills,
                    Deaths = (byte)matchParticipant.Deaths,
                    Assists = (byte)matchParticipant.Assists,
                    TotalMinionsKilled = (ushort)matchParticipant.TotalMinionsKilled,
                    FirstBloodKill = matchParticipant.FirstBloodKill,
                    FirstBloodAssist = matchParticipant.FirstBloodAssist,
                    FirstTowerKill = matchParticipant.FirstTowerKill,
                    FirstTowerAssist = matchParticipant.FirstTowerAssist,
                    LargestCriticalStrike = (ushort)matchParticipant.LargestCriticalStrike,
                    Kda = (decimal)matchParticipant.Challenges.Kda,
                    KillParticipation = (decimal)matchParticipant.Challenges.KillParticipation,
                    DoubleKills = (byte)matchParticipant.DoubleKills,
                    TripleKills = (byte)matchParticipant.TripleKills,
                    QuadraKills = (byte)matchParticipant.QuadraKills,
                    PentaKills = (byte)matchParticipant.PentaKills,
                    LargestMultiKill = (byte)matchParticipant.LargestMultiKill,
                    AllInPings = (ushort)matchParticipant.AllInPings,
                    AssistMePings = (ushort)matchParticipant.AssistMePings,
                    BasicPings = (ushort)matchParticipant.BasicPings,
                    CommandPings = (ushort)matchParticipant.CommandPings,
                    DangerPings = (ushort)matchParticipant.DangerPings,
                    EnemyMissingPings = (ushort)matchParticipant.EnemyMissingPings,
                    EnemyVisionPings = (ushort)matchParticipant.EnemyVisionPings,
                    GetBackPings = (ushort)matchParticipant.GetBackPings,
                    HoldPings = (ushort)matchParticipant.HoldPings,
                    NeedVisionPings = (ushort)matchParticipant.NeedVisionPings,
                    OnMyWayPings = (ushort)matchParticipant.OnMyWayPings,
                    PushPings = (ushort)matchParticipant.PushPings,
                    RetreatPings = (ushort)matchParticipant.RetreatPings,
                    VisionClearedPings = (ushort)matchParticipant.VisionClearedPings,
                    ControlWardTimeCoveragePercentage = (decimal)matchParticipant.Challenges.ControlWardTimeCoverageInRiverOrEnemyHalf,
                    ControlWardsPlaced = (byte)matchParticipant.Challenges.ControlWardsPlaced,
                    TotalWardsPlaced = (byte)matchParticipant.WardsPlaced,
                    TotalWardsKilled = (byte)matchParticipant.WardsKilled,
                    TotalHealing = (uint)matchParticipant.TotalHeal,
                    TotalHealingOnTeammates = (uint)matchParticipant.TotalHealsOnTeammates,
                    TotalTimeSpentDead = (ushort)matchParticipant.TotalTimeSpentDead,
                    AttackDamageTaken = (uint)matchParticipant.PhysicalDamageTaken,
                    MagicDamageTaken = (uint)matchParticipant.MagicDamageTaken,
                    TrueDamageTaken = (uint)matchParticipant.TrueDamageTaken,
                    TotalDamageTakenPercentageOfTeam = (decimal)matchParticipant.Challenges.DamageTakenOnTeamPercentage,
                    TotalDamageTaken = (uint)matchParticipant.TotalDamageTaken,
                    AttackDamageDealtToChampions = (uint)matchParticipant.PhysicalDamageDealtToChampions,
                    MagicDamageDealtToChampions = (uint)matchParticipant.MagicDamageDealtToChampions,
                    TrueDamageDealtToChampions = (uint)matchParticipant.TrueDamageDealtToChampions,
                    TotalDamageDealtToChampions = (uint)matchParticipant.TotalDamageDealtToChampions,
                    AttackDamageDealt = (uint)matchParticipant.PhysicalDamageDealt,
                    MagicDamageDealt = (uint)matchParticipant.MagicDamageDealt,
                    TrueDamageDealt = (uint)matchParticipant.TrueDamageDealt,
                    TotalDamageDealtPerMinute = (decimal)matchParticipant.Challenges.DamagePerMinute,
                    TotalDamageDealtPercentageOfTeam = (decimal)matchParticipant.Challenges.TeamDamagePercentage,
                    TotalDamageDealt = (ushort)matchParticipant.TotalDamageDealt,
                    Summoner = summoner,
                });
            }

            match.Teams.Add(team);
        }

        return match;
    }

    private static string NormalizeRiotId(string riotId)
    {
        if (string.IsNullOrWhiteSpace(riotId)) return riotId;

        riotId = riotId.Trim();
        return riotId.Length > 40 ? riotId[..40] : riotId;
    }

    private static Role ConvertRole(string role)
    {
        return role switch
        {
            "TOP" => Role.Top,
            "JUNGLE" => Role.Jungle,
            "MIDDLE" => Role.Mid,
            "BOTTOM" => Role.Bottom,
            "UTILITY" => Role.Support,
            _ => Role.None
        };
    }
}

