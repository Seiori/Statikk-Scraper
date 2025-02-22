using System.ComponentModel.DataAnnotations;

namespace Statikk_Scraper.Models;

public class Champions
{
    [Key] public short Id { get; init; }
    [MaxLength(40)] public required string Name { get; init; }
    public short PatchLastUpdated { get; init; }
    
    public ICollection<Participants> Participants { get; init; }
}