using System.Runtime.Serialization;

namespace Statikk_Scraper.Models.Enums;

public enum TimelineEventType
{
    [EnumMember(Value="ITEM_PURCHASED")]
    ItemPurchased,
    [EnumMember(Value="SKILL_LEVEL_UP")]
    SkillLevelUp,
}