using System.ComponentModel.DataAnnotations;
using Camille.Enums;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Sta.Data.Models;

public class Summoners
{
    [Key] public int Id { get; init; } 
    [MaxLength(78)] public required string Puuid { get; set; }
    [MaxLength(58)] public required string SummonerId { get; init; }
    public required PlatformRoute Platform { get; init; }
    [MaxLength(40)] public required string RiotId { get; set; }
    public required short ProfileIconId { get; init; }
    public required short SummonerLevel { get; init; }
    public DateTime LastUpdated { get; init; }
    
    public ICollection<SummonerRanks> Ranks { get; init; }
}