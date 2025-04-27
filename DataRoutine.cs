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
using Statikk_Scraper.Statikk_Scraper.Data.Models;

namespace Statikk_Scraper;

public class DataRoutine(IDbContextFactory<Context> contextFactory, RiotApiClient riotGamesApi)
{
    private const int SecondsPerDay = 86400;
    private static readonly long EndTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
    private static readonly long StartTime = EndTime - SecondsPerDay;
    private static Dictionary<string, ushort> _patchList = new();

    private static readonly Dictionary<Region, RegionalRoute> Regions = new()
    {
        [Region.NA1] = RegionalRoute.Americas,
        [Region.EUW1] = RegionalRoute.Europe,
        [Region.KR] = RegionalRoute.Asia,
    };
    private static readonly Tier[] Tiers = [Tier.CHALLENGER, Tier.GRANDMASTER, Tier.MASTER, Tier.DIAMOND, Tier.EMERALD, Tier.PLATINUM, Tier.GOLD, Tier.SILVER, Tier.BRONZE, Tier.IRON];
    private static readonly Division[] Divisions = [Division.I, Division.II, Division.III, Division.IV];

    public async Task BeginDataRoutine()
    {
        _patchList = await GetPatchesAsync();

        var regionalRouteTasks = Regions.Keys.Select(async regionalRoute =>
        {
            var tierDivisionPairs = Tiers
                .SelectMany(_ => Divisions, (tier, division) => (tier, division))
                .ToArray();

            foreach (var (tier, division) in tierDivisionPairs)
            {
                await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

                var currentPage = 1;
                var nextFetchTask = riotGamesApi.GetLeagueEntriesAsync(regionalRoute, Queue.RANKED_SOLO_5x5, tier, division, currentPage);

                while (true)
                {
                    var currentLeagueEntries = await nextFetchTask.ConfigureAwait(false);
                    if (currentLeagueEntries.Length is 0)
                    {
                        break;
                    }

                    var nextPage = currentPage + 1;
                    nextFetchTask = riotGamesApi.GetLeagueEntriesAsync(regionalRoute, Queue.RANKED_SOLO_5x5, tier, division, nextPage);

                    var summonerRanks = currentLeagueEntries.Select(le => new SummonerRanks
                    {
                        Puuid = le.Puuid,
                        Queue = 420,
                        Tier = tier,
                        Division = division,
                        LeaguePoints = (ushort)le.LeaguePoints,
                        Wins = (ushort)le.Wins,
                        Losses = (ushort)le.Losses,
                    })
                    .ToArray();

                    await ProcessPage(context, regionalRoute, Regions[regionalRoute], tier, division, currentPage, summonerRanks).ConfigureAwait(false);

                    currentPage = nextPage;
                }
            }
        });

        await Task.WhenAll(regionalRouteTasks).ConfigureAwait(false);
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

    private async Task ProcessPage(Context context, Region region, RegionalRoute regionalRoute , Tier tier, Division division, int page, SummonerRanks[] summonerRanks)
    {
        try
        {
            var matchIdList = await FetchMatchIdsFromPuuidList(context, regionalRoute, region.ToString(), summonerRanks.Select(sr => sr.Puuid)).ConfigureAwait(false);

            var matchList = (
                await Task.WhenAll(
                    matchIdList.Select(matchId => riotGamesApi.GetMatchAsync(regionalRoute, matchId)
                    )
                ).ConfigureAwait(false)
            );

            if (matchList.Length is 0) return;

            var matchesList = matchList.Select(m => ParseRiotMatchData(m, summonerRanks)).ToArray();

            var summoners = matchesList
                .SelectMany(m => m.Teams)
                .SelectMany(mt => mt.Participants)
                .Select(p => p.Summoner)
                .OfType<Summoners>()
                .ToArray();

            await context.BulkOperationAsync(BulkOperation.Upsert, summoners, options =>
            {
                options.SetOutputIdentity = true;
                options.IncludeChildEntities = true;
            }).ConfigureAwait(false);

            foreach (var participant in matchesList.SelectMany(m => m.Teams).SelectMany(mt => mt.Participants))
            {
                var summoner = summoners.First(s => s.Puuid == participant.Summoner!.Puuid);
                participant.SummonersId = summoner.Id;
            }

            await context.BulkOperationAsync(BulkOperation.Insert, matchesList, options =>
            {
                options.SetOutputIdentity = true;
                options.IncludeChildEntities = true;
                options.ExcludedNavigationPropertyNames = [nameof(Patches), nameof(Champions), nameof(Summoners), nameof(summonerRanks)];
            }).ConfigureAwait(false);

            Console.WriteLine($"Processed {matchesList.Length}'s Matches for {region} - {tier} - {division} - Page {page}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error processing page {page} for {region} - {tier} - {division}: {e.Message}");
            Console.WriteLine(e.StackTrace);
        }
    }

    private async Task<IEnumerable<string>> FetchMatchIdsFromPuuidList(Context context, RegionalRoute regionalRoute, string region, IEnumerable<string> puuidList)
    {
        var matchIdLists = await Task.WhenAll(puuidList.Select(puuid => riotGamesApi.GetMatchIdsForPuuidAsync(regionalRoute, puuid, startTime: StartTime, endTime: EndTime, queue: 420, count: 100)));

        var gameIds = matchIdLists
            .SelectMany(ids => ids)
            .Select(id => ulong.Parse(id[(id.IndexOf('_') + 1)..]))
            .ToHashSet();

        var existingGameIds = await context.Matches
            .AsNoTracking()
            .Where(m => m.Region.ToString() == region && gameIds.Contains(m.GameId))
            .Select(m => m.GameId)
            .ToHashSetAsync()
            .ConfigureAwait(false);

        return gameIds.Except(existingGameIds)
            .Select(id => $"{region}_{id}")
            .ToHashSet();
    }

    private static Matches ParseRiotMatchData(Match matchData, SummonerRanks[] summonerRanks)
    {
        var matchInfo = matchData.Info;
        var region = Enum.Parse<Region>(matchInfo.PlatformId);
        var versionParts = matchInfo.GameVersion.Split('.');
        var patchVersion = $"{versionParts[0]}.{versionParts[1]}";

        var match = new Matches
        {
            GameId       = (ulong)matchInfo.GameId,
            Region       = region,
            PatchesId    = _patchList[patchVersion],
            Queue        = (ushort)matchInfo.QueueId,
            WinningTeam  = (ushort)matchInfo.Teams.First(t => t.Win).TeamId,
            DatePlayed   = DateTimeOffset.FromUnixTimeMilliseconds(matchInfo.GameCreation).UtcDateTime,
            TimePlayed   = TimeSpan.FromSeconds(matchInfo.GameDuration),
        };

        foreach (var matchTeam in matchInfo.Teams)
        {
            var feats = matchTeam.Feats;
            var objectives = matchTeam.Objectives;
            var bans = matchTeam.Bans
                .Take(matchTeam.Bans.Length)
                .Where(ban => ban.ChampionId is not -1)
                .Select(ban => new MatchTeamBans
                {
                    ChampionsId = (ushort)ban.ChampionId,
                })
                .ToArray();

            var team = new MatchTeams
            {
                TeamId = (ushort)matchTeam.TeamId,

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

                Bans            = bans
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
                    SummonerLevel = (ushort)matchParticipant.SummonerLevel,
                    LastUpdated   = DateTime.UtcNow
                };

                var summonerRank = summonerRanks.FirstOrDefault(sr => sr.Puuid == matchParticipant.Puuid);

                if (summonerRank is not null) summoner.Ranks.Add(summonerRank);

                var summonerSpells = new List<ParticipantSummonerSpells>
                {
                    new()
                    {
                        SummonerSpellId = (ushort)matchParticipant.Summoner1Id,
                        Casts = (byte)matchParticipant.Spell1Casts,
                        SortOrder = 0
                    },
                    new()
                    {
                        SummonerSpellId = (ushort)matchParticipant.Summoner2Id,
                        Casts = (byte)matchParticipant.Spell2Casts,
                        SortOrder = 1
                    }
                };

                var itemIds = new[]
                {
                    matchParticipant.Item0, matchParticipant.Item1, matchParticipant.Item2,
                    matchParticipant.Item3, matchParticipant.Item4, matchParticipant.Item5,
                    matchParticipant.Item6
                };

                var items = itemIds
                    .Where(itemId => itemId != 0)
                    .Select(itemId => new ParticipantItems { ItemId = (ushort)itemId })
                    .ToList();

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
                    SummonerSpells = summonerSpells,
                    Items = items,
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