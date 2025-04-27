using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data.Models;

public class ParticipantItems
{
    /// <summary>
    /// Fields
    /// </summary>
    public ulong ParticipantId { get; init; }
    public ushort ItemId { get; init; }
    
    /// <summary>
    /// Foreign Keys
    /// </summary>
    public Participants? Participant { get; init; }
}