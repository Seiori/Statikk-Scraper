using Camille.Enums;
using Camille.RiotGames.MatchV5;
using Sta.Data.Models;
using Statikk_Scraper.Models;
using Team = Camille.RiotGames.Enums.Team;
using static Statikk_Scraper.Helpers.EnumExtensions;

namespace Statikk_Scraper.Helpers;

public static  class ModelHelpers
{
        public static Matches? ParseRiotMatchData(Match? matchData)
        {
            if (matchData is null)
                return null;
            
            var matchInfo    = matchData.Info;
            var versionParts = matchInfo.GameVersion.Split('.');
            var patchVersion = $"{versionParts[0]}.{versionParts[1]}";
            
            var winningTeam = matchInfo.Teams.FirstOrDefault(t => t.Win)?.TeamId ?? Team.Other;
            
            MatchTeams[] teams = [];
            if (matchInfo.QueueId is Queue.SUMMONERS_RIFT_5V5_RANKED_SOLO or Queue.SUMMONERS_RIFT_5V5_RANKED_FLEX)
            {
                // Convert the teams to an array for easier indexing
                var teamsArr = matchInfo.Teams.ToArray();
                teams = new MatchTeams[teamsArr.Length];

                for (var i = 0; i < teamsArr.Length; i++)
                {
                    var t = teamsArr[i];
                    var objectives = t.Objectives;
                    
                    var bans = t.Bans?.ToArray() ?? [];
                    var banChampionIds = bans
                        .Take(5)
                        .Select(b => (int)b.ChampionId)
                        .Concat(Enumerable.Repeat(0, 5))
                        .Take(5)
                        .ToArray();

                    teams[i] = new MatchTeams
                    {
                        Team = t.TeamId,
                        
                        Ban1 = (short)banChampionIds[0],
                        Ban2 = (short)banChampionIds[1],
                        Ban3 = (short)banChampionIds[2],
                        Ban4 = (short)banChampionIds[3],
                        Ban5 = (short)banChampionIds[4],

                        BaronKills      = (byte)objectives.Baron.Kills,
                        ChampionKills   = (byte)objectives.Champion.Kills,
                        DragonKills     = (byte)objectives.Dragon.Kills,
                        HordeKills      = (byte)(objectives.Horde?.Kills ?? 0),
                        InhibitorKills  = (byte)objectives.Inhibitor.Kills,
                        RiftHeraldKills = (byte)objectives.RiftHerald.Kills,
                        TowerKills      = (byte)objectives.Tower.Kills
                    };
                }
            }
            
            var participantsArr = matchInfo.Participants.ToArray();
            var participants = new Participants[participantsArr.Length];

            for (var i = 0; i < participantsArr.Length; i++)
            {
                var p = participantsArr[i];
                var perks = p.Perks;
                var primaryStyle   = perks.Styles.FirstOrDefault(); 
                var secondaryStyle = perks.Styles.LastOrDefault();  

                participants[i] = new Participants
                {
                    Team         = p.TeamId,
                    ChampionsId  = (short)p.ChampionId,
                    Role         = ConvertRole(p.TeamPosition),

                    PrimaryPageId           = (short)(primaryStyle?.Style ?? 0),
                    PrimaryPageKeystoneId   = (short)(primaryStyle?.Selections?[0].Perk ?? 0),
                    PrimaryPageRow1Id       = (short)(primaryStyle?.Selections?[1].Perk ?? 0),
                    PrimaryPageRow2Id       = (short)(primaryStyle?.Selections?[2].Perk ?? 0),
                    PrimaryPageRow3Id       = (short)(primaryStyle?.Selections?[3].Perk ?? 0),

                    SecondaryPageId         = (short)(secondaryStyle?.Style ?? 0),
                    SecondaryPageOption1Id  = (short)(secondaryStyle?.Selections?[0].Perk ?? 0),
                    SecondaryPageOption2Id  = (short)(secondaryStyle?.Selections?[1].Perk ?? 0),

                    OffensiveStatId = (short)perks.StatPerks.Offense,
                    DefensiveStatId = (short)perks.StatPerks.Defense,
                    FlexStatId      = (short)perks.StatPerks.Flex,

                    SummonerSpell1Id = (short)p.Summoner1Id,
                    SummonerSpell2Id = (short)p.Summoner2Id,

                    Kills  = (short)p.Kills,
                    Deaths = (short)p.Deaths,
                    Assists= (short)p.Assists,
                    Kda    = (decimal)(p.Challenges?.Kda ?? 0.0),

                    KillParticipation = (decimal)((p.Challenges?.KillParticipation ?? 0.0) * 100),
                    CreepScore        = (short)p.TotalMinionsKilled,

                    Item1Id = (short)p.Item0,
                    Item2Id = (short)p.Item1,
                    Item3Id = (short)p.Item2,
                    Item4Id = (short)p.Item3,
                    Item5Id = (short)p.Item4,
                    Item6Id = (short)p.Item5,
                    Item7Id = (short)p.Item6,

                    GoldPerMinute    = (decimal)(p.Challenges?.GoldPerMinute ?? 0.0),
                    FirstBlood       = p.FirstBloodKill,
                    DoubleKills      = (byte)p.DoubleKills,
                    TripleKills      = (byte)p.TripleKills,
                    QuadraKills      = (byte)p.QuadraKills,
                    PentaKills       = (byte)p.PentaKills,

                    DamagePerMinute  = (decimal)(p.Challenges?.DamagePerMinute ?? 0),
                    TrueDamageDealt  = p.TrueDamageDealtToChampions,
                    AttackDamageDealt= p.PhysicalDamageDealtToChampions,
                    MagicDamageDealt = p.MagicDamageDealtToChampions,

                    Summoner = new Summoners
                    {
                        Puuid         = p.Puuid,
                        SummonerId    = p.SummonerId,
                        Platform      = Enum.Parse<PlatformRoute>(matchInfo.PlatformId),
                        RiotId        = NormalizeRiotId($"{p.RiotIdGameName}#{p.RiotIdTagline}"),
                        ProfileIconId = (short)p.ProfileIcon,
                        SummonerLevel = (short)p.SummonerLevel,
                        LastUpdated = DateTime.UtcNow
                    }
                    
                };
            }
            
            if (participants.Any(p => p.Summoner.Puuid == "BOT"))
            {
                return null;
            }
            
            return new Matches
            {
                GameId       = matchInfo.GameId,
                Platform     = Enum.Parse<PlatformRoute>(matchInfo.PlatformId),
                Queue        = matchInfo.QueueId,
                DatePlayed   = DateTimeOffset.FromUnixTimeMilliseconds(matchInfo.GameCreation).UtcDateTime,
                TimePlayed   = TimeSpan.FromSeconds(matchInfo.GameDuration),
                WinningTeam  = winningTeam,
                Teams        = teams,
                Participants = participants
            };
        }
        
        private static string NormalizeRiotId(string riotId)
        {
            if (string.IsNullOrWhiteSpace(riotId))
                return riotId;

            riotId = riotId.Replace("\r", "").Replace("\n", "").Trim();
            
            return riotId.Length > 43 ? riotId[..43] : riotId;
        }
}