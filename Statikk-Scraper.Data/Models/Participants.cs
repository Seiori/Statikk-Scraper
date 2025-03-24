using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.RiotGames.Enums;
using Sta.Data.Models;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Models
{
    public class Participants
    {
        [Key] [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong Id { get; init; }
        public uint MatchesId { get; init; }
        public uint SummonersId { get; set; }
        public Team Team { get; init; }
        public ushort ChampionsId { get; init; }
        public Role Role { get; init; }
        public ushort PrimaryPageId { get; init; }
        public ushort PrimaryPageKeystoneId { get; init; }
        public ushort PrimaryPageRow1Id { get; init; }
        public ushort PrimaryPageRow2Id { get; init; }
        public ushort PrimaryPageRow3Id { get; init; }
        public ushort SecondaryPageId { get; init; }
        public ushort SecondaryPageOption1Id { get; init; }
        public ushort SecondaryPageOption2Id { get; init; }
        public ushort SummonerSpell1Id { get; init; }
        public ushort SummonerSpell2Id { get; init; }
        public ushort OffensiveStatId { get; init; }
        public ushort DefensiveStatId { get; init; }
        public ushort FlexStatId { get; init; }
        public byte Kills { get; init; }
        public byte Deaths { get; init; }
        public byte Assists { get; init; }
        public decimal Kda { get; init; }
        public byte KillParticipation { get; init; }
        public ushort CreepScore { get; init; }
        public byte CreepScorePerMinute { get; init; }
        public ushort Item1Id { get; init; }
        public ushort Item2Id { get; init; }
        public ushort Item3Id { get; init; }
        public ushort Item4Id { get; init; }
        public ushort Item5Id { get; init; }
        public ushort Item6Id { get; init; }
        public ushort Item7Id { get; init; }
        public byte LargestMultiKill { get; init; }
        public uint AttackDamageDealt { get; init; }
        public uint MagicDamageDealt { get; init; }
        public uint TrueDamageDealt { get; init; }
        
        [ForeignKey(nameof(SummonersId))]
        public required Summoners Summoner { get; set; }
        
        [ForeignKey(nameof(MatchesId))]
        public Matches? Match { get; init; }
        
        [ForeignKey(nameof(ChampionsId))]
        public Champions? Champion { get; init; }
    }
}