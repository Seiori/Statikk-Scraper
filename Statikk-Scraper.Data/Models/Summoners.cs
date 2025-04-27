using Seiori.RiotAPI.Enums;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Sta.Data.Models;

public class Summoners
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong Id { get; init; } 
    public required string Puuid { get; init; }
    public required string SummonerId { get; init; }
    public Region Region { get; init; }
    public required string RiotId { get; init; }
    public ushort ProfileIconId { get; init; }
    public ushort SummonerLevel { get; init; }
    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// Children
    /// </summary>
    public ICollection<Participants> Participants { get; init; } = [];

    public ICollection<SummonerRanks> Ranks { get; init; } = [];
}