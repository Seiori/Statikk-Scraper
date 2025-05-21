using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Statikk_Scraper;

public class AssetRoutine(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private string _wwwrootPath = string.Empty;
    
    private const string ChampionJsonUrl = "https://cdn.merakianalytics.com/riot/lol/resources/latest/en-US/champions.json";
    private const string ProfileIconJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/profile-icons.json";
    private const string SummonerSpellJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/summoner-spells.json";
    private const string SummonerSpellAssetUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/data/spells/icons2d/";
    private const string RuneJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/perks.json";
    private const string RunePageJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/perkstyles.json";
    private const string ItemJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/items.json";
    private const string ItemAssetUrl = "https://raw.communitydragon.org/latest/game/assets/items/icons2d/";

    public async Task BeginAssetRoutine(string rootPath)
    {
        _wwwrootPath = rootPath;

        try
        {
            await Task.WhenAll(
                UpdateProfileIconsAsync(),
                UpdateSummonerSpellsAsync(),
                UpdateRunesAsync(),
                UpdateItemsAsync()
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task UpdateChampionsAsync()
    {
        var iconDirs = new Dictionary<string, string>
        {
            { "Icon", Path.Combine(_wwwrootPath, "images", "Champions", "Icon") },
            { "Q", Path.Combine(_wwwrootPath, "images", "Champions", "Q") },
            { "W", Path.Combine(_wwwrootPath, "images", "Champions", "W") },
            { "E", Path.Combine(_wwwrootPath, "images", "Champions", "E") },
            { "R", Path.Combine(_wwwrootPath, "images", "Champions", "R") }
        };
        
        foreach (var dir in iconDirs.Values.Where(dir => Directory.Exists(dir) is false))
        {
            Directory.CreateDirectory(dir);
        }
        
        await using var stream = await _httpClient.GetStreamAsync(ChampionJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        foreach (var champion in doc.RootElement.EnumerateArray())
        {
            var champId = champion.GetProperty("id").GetInt16();
            var abilities = champion.GetProperty("abilities");
            var images = new Dictionary<string, string>
            {
                { "Icon", champion.GetProperty("icon").GetString() ?? string.Empty },
                { "Q", abilities.GetProperty("Q")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "W", abilities.GetProperty("W")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "E", abilities.GetProperty("E")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "R", abilities.GetProperty("R")[0].GetProperty("icon").GetString() ?? string.Empty }
            };

            foreach (var (key, url) in images)
            {
                var filePath = Path.Combine(iconDirs[key], $"{champId}.webp");
                await DownloadAndSaveImageAsync(url, filePath, $"{key} for Champion ID {champId}");
            }
        }
    }
    
    private async Task UpdateProfileIconsAsync()
    {
        var profileIconsDir = Path.Combine(_wwwrootPath, "images", "ProfileIcons");
        if (Directory.Exists(profileIconsDir) is false) Directory.CreateDirectory(profileIconsDir);
    
        await using var stream = await _httpClient.GetStreamAsync(ProfileIconJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        foreach (var profileIcon in doc.RootElement.EnumerateArray())
        {
            var iconId = profileIcon.GetProperty("id").GetInt16();
            if (!profileIcon.TryGetProperty("iconPath", out var iconPathEl)) continue;
            var iconPath = iconPathEl.GetString() ?? string.Empty;
            var imageUrl = GetAssetUrl(ProfileIconJsonUrl, iconPath);
            var filePath = Path.Combine(profileIconsDir, $"{iconId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Profile Icon ID {iconId}");
        }
    }
    
    private async Task UpdateSummonerSpellsAsync()
    {
        var spellsDir = Path.Combine(_wwwrootPath, "images", "SummonerSpells");
        if (Directory.Exists(spellsDir) is false) Directory.CreateDirectory(spellsDir);
    
        await using var stream = await _httpClient.GetStreamAsync(SummonerSpellJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        foreach (var summonerSpell in doc.RootElement.EnumerateArray().SkipLast(3))
        {
            var spellId = summonerSpell.GetProperty("id").GetInt16();
            var iconPath = summonerSpell.GetProperty("iconPath").GetString() ?? string.Empty;
            var fileName = $"{iconPath.Split('/').Last().ToLower()}";
            var imageUrl = $"{SummonerSpellAssetUrl}{fileName}";
            var filePath = Path.Combine(spellsDir, $"{spellId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Summoner Spell ID {spellId}");
        }
    }
    
    private async Task UpdateRunesAsync()
    {
        var runesDir = Path.Combine(_wwwrootPath, "images", "Runes");
        if (Directory.Exists(runesDir) is false) Directory.CreateDirectory(runesDir);
        
        await using var runeJsonStream = await _httpClient.GetStreamAsync(RuneJsonUrl);
        using var runeJsonDoc = await JsonDocument.ParseAsync(runeJsonStream);
        foreach (var rune in runeJsonDoc.RootElement.EnumerateArray())
        {
            var runeId = rune.GetProperty("id").GetInt16();
            var iconPath = rune.GetProperty("iconPath").GetString() ?? string.Empty;
            var imageUrl = GetAssetUrl(RuneJsonUrl, iconPath);
            var filePath = Path.Combine(runesDir, $"{runeId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Rune ID {runeId}");
        }
    
        await using var runePageJsonStream = await _httpClient.GetStreamAsync(RunePageJsonUrl);
        using var runePageJsonDoc = await JsonDocument.ParseAsync(runePageJsonStream);
        var styles = runePageJsonDoc.RootElement.GetProperty("styles").EnumerateArray();
        foreach (var style in styles)
        {
            var styleId = style.GetProperty("id").GetInt16();
            var iconPath = style.GetProperty("iconPath").GetString() ?? string.Empty;
            var imageUrl = GetAssetUrl(RunePageJsonUrl, iconPath);
            var filePath = Path.Combine(runesDir, $"{styleId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Rune Page ID {styleId}");
        }
    }
    
    private async Task UpdateItemsAsync()
    {
        var itemsDir = Path.Combine(_wwwrootPath, "images", "Items");
        if (Directory.Exists(itemsDir) is false) Directory.CreateDirectory(itemsDir);
    
        await using var stream = await _httpClient.GetStreamAsync(ItemJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var itemId = item.GetProperty("id").GetInt32();
            var iconPath = item.GetProperty("iconPath").GetString() ?? string.Empty;
            var fileName = $"{iconPath.Split('/').Last().ToLower()}";
            var imageUrl = $"{ItemAssetUrl}{fileName}";
            var filePath = Path.Combine(itemsDir, $"{itemId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Item ID {itemId}");
        }
    }
    
    private static string GetAssetUrl(string baseUrl, string assetPath)
    {
        var baseIndex = baseUrl.IndexOf("v1/", StringComparison.Ordinal);
        var assetIndex = assetPath.IndexOf("v1/", StringComparison.Ordinal);
        return baseUrl[..baseIndex] + assetPath[assetIndex..].ToLower();
    }
    
    private async Task DownloadAndSaveImageAsync(string imageUrl, string filePath, string contextInfo)
    {
        try
        {
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            var webpBytes = ConvertImageToWebP(imageBytes);
            await File.WriteAllBytesAsync(filePath, webpBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download image for {contextInfo}: {ex.Message}");
        }
    }
    
    private static byte[] ConvertImageToWebP(byte[] imageBytes)
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var image = Image.Load(inputStream);
        using var outputStream = new MemoryStream();
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(64, 64),
            Mode = ResizeMode.Crop
        }));
        image.Save(outputStream, new WebpEncoder { Quality = 50 });
        return outputStream.ToArray();
    }
}