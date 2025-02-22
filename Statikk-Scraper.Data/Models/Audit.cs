using System.ComponentModel.DataAnnotations;

namespace Statikk_Scraper.Models;

public class Audit
{
    [Key] public int Id { get; init; }
    [MaxLength(int.MaxValue)] public required string Method { get; init; }
    [MaxLength(int.MaxValue)] public string? Input { get; init; }
    [MaxLength(int.MaxValue)] public required string Exception { get; init; }
    [MaxLength(int.MaxValue)] public required string StackTrace { get; init; }
}