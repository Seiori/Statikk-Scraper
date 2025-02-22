using System.ComponentModel.DataAnnotations;

namespace Statikk_Scraper.Models;

public class Queues
{
    public short Id { get; init; }
    [MaxLength(40)] public required string Name { get; init; }
}