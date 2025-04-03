using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Seiori.MySql;
using Seiori.MySql.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Statikk_Scraper.Data;
using Statikk_Scraper.Models;

namespace Statikk_Scraper;

public class AssetRoutine(Context context, IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private string _wwwrootPath = string.Empty;

    private const string PatchVersionsUrl = "https://cdn.merakianalytics.com/riot/lol/resources/patches.json";
    private const string ChampionJsonUrl = "https://cdn.merakianalytics.com/riot/lol/resources/latest/en-US/champions.json";
    private const string QueueJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/queues.json";
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
            await UpdatePatchVersionsAsync();
            await UpdateChampionsAsync();
            // await UpdateProfileIconsAsync();
            // await UpdateSummonerSpellsAsync();
            // await UpdateRunesAsync();
            // await UpdateItemsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    
    private async Task<JsonDocument> GetJsonDocumentAsync(string url)
    {
        await using var stream = await _httpClient.GetStreamAsync(url);
        return await JsonDocument.ParseAsync(stream);
    }
    
    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
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

    private async Task UpdatePatchVersionsAsync()
    {
        using var doc = await GetJsonDocumentAsync(PatchVersionsUrl);
        var patches = doc.RootElement.GetProperty("patches")
            .EnumerateArray()
            .Select(p => new Patches
            {
                PatchVersion = p.GetProperty("name").GetString() ?? string.Empty,
                IsLatest = false
            })
            .Where(p => p.PatchVersion.Length <= 5)
            .DistinctBy(p => p.PatchVersion)
            .ToArray();

        if (patches.Length == 0)
            throw new Exception("Patch Versions List is Empty");

        patches.Last().IsLatest = true;
        await context.BulkOperationAsync(BulkOperation.Upsert, patches, options => { });
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
        foreach (var dir in iconDirs.Values) EnsureDirectory(dir);

        using var doc = await GetJsonDocumentAsync(ChampionJsonUrl);
        var championJson = doc.RootElement.EnumerateObject();
        var patchMapping = await context.Patches.ToDictionaryAsync(pv => pv.PatchVersion, pv => pv.Id);
        var imageTasks = new List<Task>();
        var championsList = new List<Champions>();

        foreach (var champion in championJson)
        {
            var champValue = champion.Value;
            var champId = champValue.GetProperty("id").GetInt16();
            var champName = champValue.GetProperty("name").GetString() ?? string.Empty;

            var abilities = champValue.GetProperty("abilities");
            var images = new Dictionary<string, string>
            {
                { "Icon", champValue.GetProperty("icon").GetString() ?? string.Empty },
                { "Q", abilities.GetProperty("Q")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "W", abilities.GetProperty("W")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "E", abilities.GetProperty("E")[0].GetProperty("icon").GetString() ?? string.Empty },
                { "R", abilities.GetProperty("R")[0].GetProperty("icon").GetString() ?? string.Empty }
            };

            foreach (var (key, url) in images)
            {
                var filePath = Path.Combine(iconDirs[key], $"{champId}.webp");
                imageTasks.Add(Task.Run(() => DownloadAndSaveImageAsync(url, filePath, $"{key} for Champion ID {champId}")));
            }

            championsList.Add(new Champions { Id = (ushort)champId, Name = champName });
        }
        await Task.WhenAll(imageTasks);
        if (championsList.Count == 0)
            throw new Exception("Champion List is Empty");
        await context.BulkOperationAsync(BulkOperation.Upsert, championsList, options => { });
    }

    private async Task UpdateProfileIconsAsync()
    {
        var profileIconsDir = Path.Combine(_wwwrootPath, "images", "ProfileIcons");
        EnsureDirectory(profileIconsDir);

        using var doc = await GetJsonDocumentAsync(ProfileIconJsonUrl);
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
        EnsureDirectory(spellsDir);
    
        using var summonerSpellDoc = await GetJsonDocumentAsync(SummonerSpellJsonUrl);
        foreach (var summonerSpell in summonerSpellDoc.RootElement.EnumerateArray().SkipLast(3))
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
        EnsureDirectory(runesDir);
        
        using var runeDoc = await GetJsonDocumentAsync(RuneJsonUrl);
        foreach (var rune in runeDoc.RootElement.EnumerateArray())
        {
            var runeId = rune.GetProperty("id").GetInt16();
            var iconPath = rune.GetProperty("iconPath").GetString() ?? string.Empty;
            var imageUrl = GetAssetUrl(RuneJsonUrl, iconPath);
            var filePath = Path.Combine(runesDir, $"{runeId}.webp");
            await DownloadAndSaveImageAsync(imageUrl, filePath, $"Rune ID {runeId}");
        }

        using var runePageDoc = await GetJsonDocumentAsync(RunePageJsonUrl);
        var styles = runePageDoc.RootElement.GetProperty("styles").EnumerateArray();
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
        EnsureDirectory(itemsDir);

        using var doc = await GetJsonDocumentAsync(ItemJsonUrl);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var itemId = item.GetProperty("id").GetInt32();
            var isBoots = false;
            foreach (var category in item.GetProperty("categories").EnumerateArray().Where(category => category.GetString() == "Boots")) isBoots = true;
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