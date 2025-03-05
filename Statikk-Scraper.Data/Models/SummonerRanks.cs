using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.Enums;
using Sta.Data.Models;

namespace Statikk_Scraper.Data.Models;

public class SummonerRanks
{
    [Key]
    public int SummonersId { get; set; }
    
    [Key]
    public QueueType Queue { get; set; }
    
    public Tier Tier { get; init; }
    
    public Division Division { get; init; }
    
    public short LeaguePoints { get; init; }
    
    public short Wins { get; init; }
    
    public short Losses { get; init; }
    
    public short TotalGames { get; init; }
    
    // Foreign Key Objects
    [ForeignKey("SummonersId")]
    public Summoners? Summoner { get; set; }
}