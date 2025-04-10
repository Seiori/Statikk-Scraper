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
        var platform = Enum.Parse<PlatformRoute>(matchInfo.PlatformId);
        var versionParts = matchInfo.GameVersion.Split('.');
        var patchVersion = $"{versionParts[0]}.{versionParts[1]}";

        var teamCount = matchInfo.Teams.Length;
        var teams = new MatchTeams[teamCount];
        var winningTeam = Team.Other;
        
        for (var i = 0; i < teamCount; i++)
        {
            var team = matchInfo.Teams[i];
            if (team.Win) winningTeam = team.TeamId;
            
            var objectives = team.Objectives;
            
            var banChampionIds = new int[5];
            var bansToProcess = Math.Min(team.Bans.Length, 5);
            for (var j = 0; j < bansToProcess; j++)
            {
                banChampionIds[j] = (ushort)team.Bans[j].ChampionId;
            }
            
            teams[i] = new MatchTeams
            {
                Team = team.TeamId,

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
        
        var participantCount = matchInfo.Participants.Length;
        var participants = new Participants[participantCount];
        
        for (var i = 0; i < participantCount; i++)
        {
            var participant = matchInfo.Participants[i];
            var perks = participant.Perks;
            var primaryStyle = perks.Styles.FirstOrDefault();
            var secondaryStyle = perks.Styles.LastOrDefault();

            var summoner = new Summoners
            {
                Puuid         = participant.Puuid,
                SummonerId    = participant.SummonerId,
                Platform      = platform,
                RiotId        = NormalizeRiotId($"{participant.RiotIdGameName}#{participant.RiotIdTagline}"),
                ProfileIconId = (ushort)participant.ProfileIcon,
                SummonerLevel = (ushort)participant.SummonerLevel,
                LastUpdated   = DateTime.UtcNow,
                Ranks = new List<SummonerRanks>()
            };

            participants[i] = new Participants
            {
                Team         = participant.TeamId,
                ChampionsId  = (ushort)participant.ChampionId,
                Role         = ConvertRole(participant.TeamPosition),
                
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
                FlexStatId      = (ushort)perks.StatPerks.Flex,
                
                SummonerSpell1Id = (ushort)participant.Summoner1Id,
                SummonerSpell2Id = (ushort)participant.Summoner2Id,

                Kills   = (byte)participant.Kills,
                Deaths  = (byte)participant.Deaths,
                Assists = (byte)participant.Assists,
                Kda     = (decimal)(participant.Challenges?.Kda ?? 0.0),

                KillParticipation = (byte)((participant.Challenges?.KillParticipation ?? 0.0) * 100),
                CreepScore        = (ushort)participant.TotalMinionsKilled,

                Item1Id = (ushort)participant.Item0,
                Item2Id = (ushort)participant.Item1,
                Item3Id = (ushort)participant.Item2,
                Item4Id = (ushort)participant.Item3,
                Item5Id = (ushort)participant.Item4,
                Item6Id = (ushort)participant.Item5,
                Item7Id = (ushort)participant.Item6,
                
                LargestMultiKill = (byte)participant.LargestMultiKill,
                
                AttackDamageDealt = (uint)participant.PhysicalDamageDealtToChampions,
                MagicDamageDealt = (uint)participant.MagicDamageDealtToChampions,
                TrueDamageDealt = (uint)participant.TrueDamageDealtToChampions,

                Summoner = summoner
            };
        }
        
        return new Matches
        {
            GameId       = (ulong)matchInfo.GameId,
            Platform     = Enum.Parse<PlatformRoute>(matchInfo.PlatformId),
            Queue        = matchInfo.QueueId,
            DatePlayed   = DateTimeOffset.FromUnixTimeMilliseconds(matchInfo.GameCreation).UtcDateTime,
            TimePlayed   = TimeSpan.FromSeconds(matchInfo.GameDuration),
            WinningTeam  = winningTeam,
            Teams        = teams,
            Participants = participants,
            Patch        = new Patches { PatchVersion = patchVersion }
        };
    }

    private static string NormalizeRiotId(string riotId)
    {
        if (string.IsNullOrWhiteSpace(riotId)) return riotId;

        riotId = riotId.Trim();
        return riotId.Length > 40 ? riotId[..40] : riotId;
    }
}