using Statikk_Scraper.Models;

namespace Statikk_Scraper.Statikk_Scraper.Data.Models;

public class ParticipantSummonerSpells
{
    public ulong ParticipantId { get; init; }
    public ushort SummonerSpellId { get; init; }
    public byte Casts { get; init; }
    public byte SortOrder { get; init; }
    
    public Participants? Participant { get; init; }
}