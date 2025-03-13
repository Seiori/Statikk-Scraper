using System.ComponentModel.DataAnnotations;
using Camille.Enums;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Sta.Data.Models;

public class Summoners
{
    [Key] public int Id { get; set; } 
    [MaxLength(78)] public required string Puuid { get; set; }
    [MaxLength(58)] public required string SummonerId { get; set; }
    public required PlatformRoute Platform { get; set; }
    [MaxLength(40)] public required string RiotId { get; set; }
    public required short ProfileIconId { get; set; }
    public required short SummonerLevel { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public ICollection<SummonerRanks> Ranks { get; set; }
}