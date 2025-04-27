using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data.Models;

public class MatchTeamBans
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong MatchTeamsId { get; init; }
    public ushort ChampionsId { get; init; }
    
    /// <summary>
    /// Foreign Keys
    /// </summary>
    public MatchTeams? MatchTeam { get; init; }
    public Champions? Champion { get; init; }
}