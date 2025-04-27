using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models.Enums;
using Statikk_Scraper.Statikk_Scraper.Data.Models;

namespace Statikk_Scraper.Models
{
    public class Participants
    {
        /// <summary>
        /// Fields
        /// </summary>
        public ulong Id { get; init; }
        public ulong SummonersId { get; set; }
        public ulong MatchTeamsId { get; init; }
        public Role Role { get; init; }
        public ushort ChampionsId { get; init; }
        public byte ChampionLevel { get; init; }
        public ushort PrimaryPageId { get; init; }
        public ushort PrimaryPageKeystoneId { get; init; }
        public ushort PrimaryPageRow1Id { get; init; }
        public ushort PrimaryPageRow2Id { get; init; }
        public ushort PrimaryPageRow3Id { get; init; }
        public ushort SecondaryPageId { get; init; }
        public ushort SecondaryPageOption1Id { get; init; }
        public ushort SecondaryPageOption2Id { get; init; }
        public ushort OffensiveStatId { get; init; }
        public ushort DefensiveStatId { get; init; }
        public ushort FlexStatId { get; init; }
        public ushort TotalQCasts { get; init; }
        public ushort TotalWCasts { get; init; }
        public ushort TotalECasts { get; init; }
        public ushort TotalRCasts { get; init; }
        public byte Kills { get; init; }
        public byte Deaths { get; init; }
        public byte Assists { get; init; }
        public ushort TotalMinionsKilled { get; init; }
        public bool FirstBloodKill { get; init; }
        public bool FirstBloodAssist { get; init; }
        public bool FirstTowerKill { get; init; }
        public bool FirstTowerAssist { get; init; }
        public ushort LargestCriticalStrike { get; init; }
        public decimal Kda { get; init; }
        public decimal KillParticipation { get; init; }
        public byte DoubleKills { get; init; }
        public byte TripleKills { get; init; }
        public byte QuadraKills { get; init; }
        public byte PentaKills { get; init; }
        public byte LargestMultiKill { get; init; }
        public ushort AllInPings { get; init; }
        public ushort AssistMePings { get; init; }
        public ushort BasicPings { get; init; }
        public ushort CommandPings { get; init; }
        public ushort DangerPings { get; init; }
        public ushort EnemyMissingPings { get; init; }
        public ushort EnemyVisionPings { get; init; }
        public ushort GetBackPings { get; init; }
        public ushort HoldPings { get; init; }
        public ushort NeedVisionPings { get; init; }
        public ushort OnMyWayPings { get; init; }
        public ushort PushPings { get; init; }
        public ushort RetreatPings { get; init; }
        public ushort VisionClearedPings { get; init; }
        public decimal ControlWardTimeCoveragePercentage { get; init; }
        public byte ControlWardsPlaced { get; init; }
        public byte TotalWardsPlaced { get; init; }
        public byte TotalWardsKilled { get; init; }
        public uint TotalHealing { get; init; }
        public uint TotalHealingOnTeammates { get; init; }
        public ushort TotalTimeSpentDead { get; init; }
        public uint AttackDamageTaken { get; init; }
        public uint MagicDamageTaken { get; init; }
        public uint TrueDamageTaken { get; init; }
        public decimal TotalDamageTakenPercentageOfTeam { get; init; }
        public uint TotalDamageTaken { get; init; }
        public uint AttackDamageDealtToChampions { get; init; }
        public uint MagicDamageDealtToChampions { get; init; }
        public uint TrueDamageDealtToChampions { get; init; }
        public uint TotalDamageDealtToChampions { get; init; }
        public uint AttackDamageDealt { get; init; }
        public uint MagicDamageDealt { get; init; }
        public uint TrueDamageDealt { get; init; }
        public decimal TotalDamageDealtPerMinute { get; init; }
        public decimal TotalDamageDealtPercentageOfTeam { get; init; }
        public uint TotalDamageDealt { get; init; }
        
        /// <summary>
        /// Foreign Keys
        /// </summary>
        public Summoners? Summoner { get; init; }
        public MatchTeams? Team { get; init; }
        public Champions? Champion { get; init; }

        /// <summary>
        /// Children
        /// </summary>
        public ICollection<ParticipantSummonerSpells> SummonerSpells { get; init; } = [];
        public ICollection<ParticipantItems> Items { get; init; } = [];
    }
}