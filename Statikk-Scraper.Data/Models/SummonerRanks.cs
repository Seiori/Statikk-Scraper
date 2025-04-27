using Seiori.RiotAPI.Enums;
using Sta.Data.Models;

namespace Statikk_Scraper.Data.Models;

public class SummonerRanks
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong SummonersId { get; set; }
    
    public string Puuid { get; init; } = string.Empty;
    public ushort Queue { get; init; }
    public Tier Tier { get; init; }
    public Division Division { get; init; }
    public ushort LeaguePoints { get; init; }
    public ushort Wins { get; init; }
    public ushort Losses { get; init; }
    
    /// <summary>
    /// Foreign Keys
    /// </summary>
    public Summoners? Summoner { get; init; }
}