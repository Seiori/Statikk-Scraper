using System.ComponentModel.DataAnnotations.Schema;
using Camille.RiotGames.Enums;
using Sta.Data.Models;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Models
{
    public class Participants
    {
        public int SummonersId { get; set; }
        public int MatchesId { get; init; }
        public short ChampionsId { get; init; }
        public Role Role { get; init; }
        public Team Team { get; init; }
        public short PrimaryPageId { get; init; }
        public short PrimaryPageKeystoneId { get; init; }
        public short PrimaryPageRow1Id { get; init; }
        public short PrimaryPageRow2Id { get; init; }
        public short PrimaryPageRow3Id { get; init; }
        public short SecondaryPageId { get; init; }
        public short SecondaryPageOption1Id { get; init; }
        public short SecondaryPageOption2Id { get; init; }
        public short SummonerSpell1Id { get; init; }
        public short SummonerSpell2Id { get; init; }
        public short OffensiveStatId { get; init; }
        public short DefensiveStatId { get; init; }
        public short FlexStatId { get; init; }
        public byte Kills { get; init; }
        public byte Deaths { get; init; }
        public byte Assists { get; init; }
        public decimal Kda { get; init; }
        public byte KillParticipation { get; init; }
        public short CreepScore { get; init; }
        public short Item1Id { get; init; }
        public short Item2Id { get; init; }
        public short Item3Id { get; init; }
        public short Item4Id { get; init; }
        public short Item5Id { get; init; }
        public short Item6Id { get; init; }
        public short Item7Id { get; init; }
        public byte LargestMultiKill { get; init; }
        public int AttackDamageDealt { get; init; }
        public int MagicDamageDealt { get; init; }
        public int TrueDamageDealt { get; init; }
        
        [ForeignKey(nameof(SummonersId))]
        public Summoners Summoner { get; set; }
        
        [ForeignKey(nameof(MatchesId))]
        public Matches Match { get; init; }
        
        [ForeignKey(nameof(ChampionsId))]
        public Champions Champion { get; init; }
    }
}