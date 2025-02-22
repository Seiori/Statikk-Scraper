using System.ComponentModel.DataAnnotations;
using Camille.Enums;
using Team = Camille.RiotGames.Enums.Team;

namespace Statikk_Scraper.Models
{
    public class Matches
    {
        [Key] public int Id { get; init; }
        public PlatformRoute Platform { get; init; }
        public long GameId { get; init; }
        public short PatchVersionsId { get; init; }
        public Queue Queue { get; init; }
        public Tier Tier { get; init; }
        public Division Division { get; init; }
        public DateTime DatePlayed { get; init; }
        public TimeSpan TimePlayed { get; init; }
        public Team WinningTeam { get; init; }
        
        public PatchVersions PatchVersion { get; init; }
        public ICollection<MatchTeams> Teams { get; init; }
        public ICollection<Participants> Participants { get; init; }
    }
}