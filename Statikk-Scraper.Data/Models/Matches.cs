using Seiori.RiotAPI.Enums;

namespace Statikk_Scraper.Models
{
    public class Matches
    {
        /// <summary>
        /// Fields
        /// </summary>
        public ulong Id { get; init; }
        public Region Region { get; init; }
        public ulong GameId { get; init; }  
        public ushort PatchesId { get; init; }
        public ushort Queue { get; init; }
        public Tier Tier { get; set; }
        public Division Division { get; set; }
        public ushort LeaguePoints { get; set; }
        public DateTime DatePlayed { get; init; }
        public TimeSpan TimePlayed { get; init; }
        public string EndOfGameResult { get; init; }
        
        /// <summary>
        /// Foreign Keys
        /// </summary>
        public Patches? Patch { get; init; }

        /// <summary>
        /// Children
        /// </summary>
        public ICollection<Teams> Teams { get; init; } = [];
    }
}