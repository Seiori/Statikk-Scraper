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
        public Tier Tier { get; init; }
        public Division Division { get; init; }
        public ushort LeaguePoints { get; init; }
        public ushort WinningTeam { get; init; }
        public ushort GameEndedInEarlySurrender { get; init; }
        public ushort GameEndedInSurrender { get; init; }
        public DateTime DatePlayed { get; init; }
        public TimeSpan TimePlayed { get; init; }
        
        /// <summary>
        /// Foreign Keys
        /// </summary>
        public Patches? Patch { get; init; }

        /// <summary>
        /// Children
        /// </summary>
        public ICollection<MatchTeams> Teams { get; init; } = [];
    }
}