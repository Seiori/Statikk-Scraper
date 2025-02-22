using Camille.RiotGames.Enums;

namespace Statikk_Scraper.Models;

public class MatchTeams
{
    public int MatchesId { get; set; }
    public Team Team { get; init; }
    public short Ban1 { get; init; }
    public short Ban2 { get; init; }
    public short Ban3 { get; init; }
    public short Ban4 { get; init; }
    public short Ban5 { get; init; }
    public byte BaronKills { get; init; }
    public byte ChampionKills { get; init; }
    public byte DragonKills { get; init; }
    public byte HordeKills { get; init; }
    public byte InhibitorKills { get; init; }
    public byte RiftHeraldKills { get; init; }
    public byte TowerKills { get; init; }
    
    public Matches Match { get; init; }
}