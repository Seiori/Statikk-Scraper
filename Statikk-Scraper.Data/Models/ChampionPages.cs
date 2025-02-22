using Camille.Enums;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Models;

public class ChampionPages
{
    public short ChampionsId { get; init; }
    public Role Role { get; init; }
    public PlatformRoute Platform { get; init; }
    public short PatchVersionsId { get; init; }
    public Queue Queue { get; init; }
    public Tier Tier { get; init; }
    public Division Division { get; init; }
    public decimal WinRate { get; init; }
    public decimal PickRate { get; init; }
    public short PrimaryPageId { get; init; }
    public short PrimaryPageKeystoneId { get; init; }
    public short PrimaryPageRow1Id { get; init; }
    public short PrimaryPageRow2Id { get; init; }
    public short PrimaryPageRow3Id { get; init; }
    public short SecondaryPageId { get; init; }
    public short SecondaryPageOption1Id { get; init; }
    public short SecondaryPageOption2Id { get; init; }
    public short OffensiveStatId { get; init; }
    public short DefensiveStatId { get; init; }
    public short FlexStatId { get; init; }
    public decimal RunePageWinRate { get; init; }
    public decimal RunePagePickRate { get; init; }
    public int RunePageTotalGames { get; init; }
    public short SummonerSpell1Id { get; init; }
    public short SummonerSpell2Id { get; init; }
    public decimal SummonerSpellsWinRate { get; init; }
    public decimal SummonerSpellsPickRate { get; init; }
    public int SummonerSpellsTotalGames { get; init; }
    public decimal AttackDamagePercent { get; init; }
    public decimal MagicDamagePercent { get; init; }
    public decimal TrueDamagePercent { get; init; }
    public decimal Pbi { get; init; }
    public int TotalGames { get; init; }
    
    public Champions Champion { get; init; }
}