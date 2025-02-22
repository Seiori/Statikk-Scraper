using System.ComponentModel.DataAnnotations;
using Camille.RiotGames.Enums;
using Sta.Data.Models;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Models
{
    public class Participants
    {
        [Key] public int Id { get; init; }
        public int SummonersId { get; set; }
        public int MatchesId { get; set; }
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
        public short OffensiveStatId { get; init; }
        public short DefensiveStatId { get; init; }
        public short FlexStatId { get; init; }
        public short SummonerSpell1Id { get; init; }
        public short SummonerSpell2Id { get; init; }
        public short Kills { get; init; }
        public short Deaths { get; init; }
        public short Assists { get; init; }
        public decimal Kda { get; init; }
        public decimal KillParticipation { get; init; }
        public short CreepScore { get; init; }
        public short Item1Id { get; init; }
        public short Item2Id { get; init; }
        public short Item3Id { get; init; }
        public short Item4Id { get; init; }
        public short Item5Id { get; init; }
        public short Item6Id { get; init; }
        public short Item7Id { get; init; }
        public decimal GoldPerMinute { get; init; }
        public bool FirstBlood { get; init; }
        public byte DoubleKills { get; init; }
        public byte TripleKills { get; init; }
        public byte QuadraKills { get; init; }
        public byte PentaKills { get; init; }
        public decimal DamagePerMinute { get; init; }
        public int TrueDamageDealt { get; init; }
        public int AttackDamageDealt { get; init; }
        public int MagicDamageDealt { get; init; }
        
        public Summoners Summoner { get; set; }
        public Matches Match { get; init; }
        public Champions Champion { get; init; }
    }
}