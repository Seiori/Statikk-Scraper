using Seiori.RiotAPI.Enums;
using Sta.Data.Models;

namespace Statikk_Scraper.Data.Models;

public class SummonerRanks
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong SummonersId { get; set; }
    public byte Season { get; set; }
    public Queue Queue { get; init; }
    public DateOnly Date { get; init; }
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