using Camille.Enums;
using Camille.RiotGames.MatchV5;
using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;
using Team = Camille.RiotGames.Enums.Team;
using static Statikk_Scraper.Helpers.EnumExtensions;

namespace Statikk_Scraper.Helpers;

public static  class ModelHelpers
{
    public static Matches ParseRiotMatchData(Match matchData)
    {
        var matchInfo = matchData.Info;
        var versionParts = matchInfo.GameVersion.Split('.');
        var patchVersion = $"{versionParts[0]}.{versionParts[1]}";
        
        var teamsArr = matchInfo.Teams.ToArray();
        var winningTeam = teamsArr.FirstOrDefault(t => t.Win)?.TeamId ?? Team.Other;
        
        var teams = new MatchTeams[teamsArr.Length];
        for (var i = 0; i < teamsArr.Length; i++)
        {
            var t = teamsArr[i];
            var objectives = t.Objectives;
            
            var banChampionIds = new int[5];
            {
                var j = 0;
                foreach (var ban in t.Bans)
                {
                    if (j >= 5)
                        break;
                    banChampionIds[j++] = (int)ban.ChampionId;
                }
            }

            teams[i] = new MatchTeams
            {
                Team = t.TeamId,

                Ban1 = (ushort)banChampionIds[0],
                Ban2 = (ushort)banChampionIds[1],
                Ban3 = (ushort)banChampionIds[2],
                Ban4 = (ushort)banChampionIds[3],
                Ban5 = (ushort)banChampionIds[4],

                BaronKills      = (byte)objectives.Baron.Kills,
                ChampionKills   = (byte)objectives.Champion.Kills,
                DragonKills     = (byte)objectives.Dragon.Kills,
                HordeKills      = (byte)(objectives.Horde?.Kills ?? 0),
                InhibitorKills  = (byte)objectives.Inhibitor.Kills,
                RiftHeraldKills = (byte)objectives.RiftHerald.Kills,
                TowerKills      = (byte)objectives.Tower.Kills
            };
        }
        
        var participantsArr = matchInfo.Participants.ToArray();
        var participants = new Participants[participantsArr.Length];
        for (var i = 0; i < participantsArr.Length; i++)
        {
            var p = participantsArr[i];
            var perks = p.Perks;
            var primaryStyle = perks.Styles.FirstOrDefault();
            var secondaryStyle = perks.Styles.LastOrDefault();

            var summoner = new Summoners
            {
                Puuid         = p.Puuid,
                SummonerId    = p.SummonerId,
                Platform      = Enum.Parse<PlatformRoute>(matchInfo.PlatformId),
                RiotId        = NormalizeRiotId($"{p.RiotIdGameName}#{p.RiotIdTagline}"),
                ProfileIconId = (ushort)p.ProfileIcon,
                SummonerLevel = (ushort)p.SummonerLevel,
                LastUpdated   = DateTime.UtcNow,
                Ranks = new List<SummonerRanks>()
            };

            participants[i] = new Participants
            {
                Team         = p.TeamId,
                ChampionsId  = (ushort)p.ChampionId,
                Role         = ConvertRole(p.TeamPosition),
                
                PrimaryPageId           = (ushort)(primaryStyle?.Style ?? 0),
                PrimaryPageKeystoneId   = (ushort)(primaryStyle?.Selections[0].Perk ?? 0),
                PrimaryPageRow1Id       = (ushort)(primaryStyle?.Selections[1].Perk ?? 0),
                PrimaryPageRow2Id       = (ushort)(primaryStyle?.Selections[2].Perk ?? 0),
                PrimaryPageRow3Id       = (ushort)(primaryStyle?.Selections[3].Perk ?? 0),
                SecondaryPageId         = (ushort)(secondaryStyle?.Style ?? 0),
                SecondaryPageOption1Id  = (ushort)(secondaryStyle?.Selections[0].Perk ?? 0),
                SecondaryPageOption2Id  = (ushort)(secondaryStyle?.Selections[1].Perk ?? 0),
                
                OffensiveStatId = (ushort)perks.StatPerks.Offense,
                DefensiveStatId = (ushort)perks.StatPerks.Defense,
                FlexStatId = (ushort)perks.StatPerks.Flex,
                
                SummonerSpell1Id = (ushort)p.Summoner1Id,
                SummonerSpell2Id = (ushort)p.Summoner2Id,

                Kills  = (byte)p.Kills,
                Deaths = (byte)p.Deaths,
                Assists= (byte)p.Assists,
                Kda    = (decimal)(p.Challenges?.Kda ?? 0.0),

                KillParticipation = (byte)((p.Challenges?.KillParticipation ?? 0.0) * 100),
                CreepScore        = (ushort)p.TotalMinionsKilled,

                Item1Id = (ushort)p.Item0,
                Item2Id = (ushort)p.Item1,
                Item3Id = (ushort)p.Item2,
                Item4Id = (ushort)p.Item3,
                Item5Id = (ushort)p.Item4,
                Item6Id = (ushort)p.Item5,
                Item7Id = (ushort)p.Item6,
                
                LargestMultiKill = (byte)p.LargestMultiKill,
                
                AttackDamageDealt = (uint)p.PhysicalDamageDealtToChampions,
                MagicDamageDealt = (uint)p.MagicDamageDealtToChampions,
                TrueDamageDealt = (uint)p.TrueDamageDealtToChampions,

                Summoner = summoner
            };
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
        return riotId.Length > 40 ? riotId[..40] : riotId;
    }
}