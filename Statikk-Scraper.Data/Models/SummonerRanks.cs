using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.Enums;
using Sta.Data.Models;

namespace Statikk_Scraper.Data.Models;

public class SummonerRanks
{
    [Key]
    public uint SummonersId { get; set; }
    
    [Key]
    public QueueType Queue { get; init; }
    
    public Tier Tier { get; init; }
    
    public Division Division { get; init; }
    
    public ushort LeaguePoints { get; init; }
    
    public ushort Wins { get; init; }
    
    public ushort Losses { get; init; }
    
    [ForeignKey(nameof(SummonersId))]
    public Summoners? Summoner { get; init; }
}