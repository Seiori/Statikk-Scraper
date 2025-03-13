using Camille.Enums;

namespace Statikk_Scraper.Data.Models;

public class SummonerRanks
{
    public int SummonersId { get; set; }
    public QueueType Queue { get; set; }
    public Tier Tier { get; init; }
    public Division Division { get; init; }
    public short LeaguePoints { get; init; }
    public short Wins { get; init; }
    public short Losses { get; init; }
    public short TotalGames { get; init; }
}