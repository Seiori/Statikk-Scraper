using System.ComponentModel.DataAnnotations;

namespace Statikk_Scraper.Models;

public class PatchVersions
{
    [Key] public short Id { get; init; }
    [MaxLength(5)] public required string PatchVersion { get; init; }
    public long StartTimeStamp { get; init; }
    public bool IsLatest { get; set; } 
    
    public ICollection<Matches> Matches { get; init; }
}