using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Statikk_Scraper.Models;

public class Audit
{
    [Key] [MaxLength(256)] 
    public required string Method { get; init; }

    [MaxLength(int.MaxValue)] 
    public string Input { get; init; } = string.Empty;

    [MaxLength(int.MaxValue)] 
    public required string Exception { get; init; }

    [MaxLength(int.MaxValue)] 
    public required string StackTrace { get; init; }

    [Key] [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime Date { get; init; }
}