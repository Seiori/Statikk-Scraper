namespace Statikk_Scraper.Models;

public class Teams
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong Id { get; init; }
    public ulong MatchesId { get; init; }
    public ushort TeamId { get; init; }
    public ushort Ban1 { get; init; }
    public ushort Ban2 { get; init; }
    public ushort Ban3 { get; init; }
    public ushort Ban4 { get; init; }
    public ushort Ban5 { get; init; }
    public ushort EpicMonsterKill { get; init; }
    public ushort FirstBlood { get; init; }
    public ushort FirstTurret { get; init; }
    public byte AtakhanKills { get; init; }
    public byte BaronKills { get; init; }
    public byte ChampionKills { get; init; }
    public byte DragonKills { get; init; }
    public byte HordeKills { get; init; }
    public byte InhibitorKills { get; init; }
    public byte RiftHeraldKills { get; init; }
    public byte TowerKills { get; init; }
    
    /// <summary>
    /// Foreign Keys
    /// </summary>
    public Matches? Match { get; init; }

    /// <summary>
    /// Children
    /// </summary>
    public ICollection<Participants> Participants { get; init; } = [];
}