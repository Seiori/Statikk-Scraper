using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Data.Models;

public class ParticipantItems
{
    public long ParticipantsId { get; set; }
    public ItemType ItemType { get; set; }
    public short Id { get; set; }
}