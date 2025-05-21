using Statikk_Scraper.Data.Models;

namespace Statikk_Scraper.Models;

public class Champions
{
    /// <summary>
    /// Fields
    /// </summary>
    public ushort Id { get; init; }
    public required string Name { get; init; }

    /// <summary>
    /// Children
    /// </summary>
    public ICollection<Participants> Participants { get; init; } = [];
}