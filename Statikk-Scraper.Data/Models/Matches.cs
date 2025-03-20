using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.Enums;
using Team = Camille.RiotGames.Enums.Team;

namespace Statikk_Scraper.Models
{
    public class Matches
    {
        [Key] [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public uint Id { get; init; }
        
        public PlatformRoute Platform { get; init; }
        
        public long GameId { get; init; }
        
        public short PatchesId { get; init; }
        
        public Queue Queue { get; init; }
        
        public Tier Tier { get; init; }
        
        public Division Division { get; init; }
        
        public ushort LeaguePoints { get; init; }
        
        public Team WinningTeam { get; init; }
        
        public DateTime DatePlayed { get; init; }
        
        public TimeSpan TimePlayed { get; init; }
        
        [ForeignKey(nameof(PatchesId))]
        public Patches? Patch { get; init; }
        
        public required ICollection<MatchTeams> Teams { get; init; }
        
        public required ICollection<Participants> Participants { get; init; }
    }
}