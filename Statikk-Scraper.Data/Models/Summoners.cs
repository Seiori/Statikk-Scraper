using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.Enums;
using Statikk_Scraper.Data.Models;

namespace Sta.Data.Models;

public class Summoners
{
    [Key] [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public uint Id { get; init; } 
    
    [MaxLength(78)] 
    public required string Puuid { get; init; }
    
    [MaxLength(58)]
    public required string SummonerId { get; init; }
    
    public PlatformRoute Platform { get; init; }
    
    [MaxLength(40)] 
    public required string RiotId { get; init; }
    
    public ushort ProfileIconId { get; init; }
    
    public ushort SummonerLevel { get; init; }
    
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime LastUpdated { get; init; }
    
    public ICollection<SummonerRanks>? Ranks { get; init; }
}