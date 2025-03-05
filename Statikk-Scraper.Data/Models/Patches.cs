using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Statikk_Scraper.Models;

public class Patches
{
    [Key] 
    public short Id { get; init; }
    
    [MaxLength(5)] 
    public required string PatchVersion { get; init; }
    
    public bool IsLatest { get; set; }
}