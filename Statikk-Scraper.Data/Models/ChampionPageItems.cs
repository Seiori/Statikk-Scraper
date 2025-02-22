using System.ComponentModel.DataAnnotations;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Models;

public class ChampionPageItems
{
    [Key] public int ChampionPageId { get; init; }
    public ItemType ItemType { get; init; }
    public short ItemId { get; init; }
    public decimal WinRate { get; init; }
    public decimal PickRate { get; init; }
    public int TotalGames { get; init; }
}