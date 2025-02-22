using System.Reflection;
using System.Runtime.Serialization;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Helpers;

public static class EnumExtensions
{
    public static string GetEnumMemberValue<T>(this T enumValue) where T : Enum
    {
        var enumType = typeof(T);
        var enumName = enumType.GetEnumName(enumValue);
        var member = enumType.GetField(enumName);

        var attribute = member?.GetCustomAttribute<EnumMemberAttribute>();
        return attribute?.Value ?? enumName;
    }
    
    public static Role ConvertRole(string role)
    {
        return role switch
        {
            "TOP" => Role.Top,
            "JUNGLE" => Role.Jungle,
            "MIDDLE" => Role.Mid,
            "BOTTOM" => Role.Bot,
            "UTILITY" => Role.Support,
            _ => Role.None
        };
    }
}