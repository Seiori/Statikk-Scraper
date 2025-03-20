using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Statikk_Scraper.Models;

public class Champions
{
    [Key] [DatabaseGenerated(DatabaseGeneratedOption.None)] 
    public ushort Id { get; init; }
    
    [MaxLength(40)] 
    public required string Name { get; init; }
}