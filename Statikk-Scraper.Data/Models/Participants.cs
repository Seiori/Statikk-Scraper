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
        public short PrimaryPageKeystoneId { get; init; }
        public short SecondaryPageId { get; init; }
        public short SummonerSpell1Id { get; init; }
        public short SummonerSpell2Id { get; init; }
        public byte Kills { get; init; }
        public byte Deaths { get; init; }
        public byte Assists { get; init; }
        public decimal Kda { get; init; }
        public byte KillParticipation { get; init; }
        public short CreepScore { get; init; }
        public decimal CreepScorePerMinute { get; init; }
        public short Item1Id { get; init; }
        public short Item2Id { get; init; }
        public short Item3Id { get; init; }
        public short Item4Id { get; init; }
        public short Item5Id { get; init; }
        public short Item6Id { get; init; }
        public short Item7Id { get; init; }
        public byte LargestMultiKill { get; init; }
        public int DamageDealt { get; init; }
        public int DamageTaken { get; init; }
        
        public Summoners Summoner { get; set; }
        public Matches Match { get; init; }
        public Champions Champion { get; init; }
    }
}