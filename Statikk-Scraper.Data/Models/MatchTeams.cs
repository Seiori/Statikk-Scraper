using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camille.RiotGames.Enums;

namespace Statikk_Scraper.Models;

public class MatchTeams
{
    [Key]
    public uint MatchesId { get; init; }
    
    [Key]
    public Team Team { get; init; }
    
    public ushort Ban1 { get; init; }
    
    public ushort Ban2 { get; init; }
    
    public ushort Ban3 { get; init; }
    
    public ushort Ban4 { get; init; }
    
    public ushort Ban5 { get; init; }
    
    public byte BaronKills { get; init; }
    
    public byte ChampionKills { get; init; }
    
    public byte DragonKills { get; init; }
    
    public byte HordeKills { get; init; }
    
    public byte InhibitorKills { get; init; }
    
    public byte RiftHeraldKills { get; init; }
    
    public byte TowerKills { get; init; }
    
    [ForeignKey(nameof(MatchesId))]
    public Matches? Match { get; init; }
}