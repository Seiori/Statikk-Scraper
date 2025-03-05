using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.Enums;
using Team = Camille.RiotGames.Enums.Team;

namespace Statikk_Scraper.Models
{
    public class Matches
    {
        [Key] [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; init; }
        
        public PlatformRoute Platform { get; init; }
        
        public long GameId { get; init; }
        
        public short PatchesId { get; init; }
        
        public Queue Queue { get; init; }
        
        public Tier Tier { get; set; }
        
        public Division Division { get; set; }
        
        public short LeaguePoints { get; set; }
        
        public DateTime DatePlayed { get; init; }
        
        public TimeSpan TimePlayed { get; init; }
        
        public Team WinningTeam { get; init; }
        
        // Foreign Key Objects
        [ForeignKey(nameof(PatchesId))]
        public Patches? Patch { get; init; }
        
        public required ICollection<MatchTeams> Teams { get; init; }
        
        public required ICollection<Participants> Participants { get; init; }
    }
}