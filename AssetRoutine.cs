using System.Text.Json;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Statikk_Scraper.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Statikk_Scraper.Data;

namespace Statikk_Scraper;

public static class AssetRoutine
{
    private static readonly HttpClient HttpClient = new();
    private static string _wwwrootPath = string.Empty;
    private static string _connectionString = string.Empty;
    
    private const string PatchVersionsUrl = "https://cdn.merakianalytics.com/riot/lol/resources/patches.json";
    private const string ChampionJsonUrl = "https://cdn.merakianalytics.com/riot/lol/resources/latest/en-US/champions.json";
    private const string QueueJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/queues.json";
    private const string ProfileIconJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/profile-icons.json";
    private const string SummonerSpellJsonUrl = "https://ddragon.leagueoflegends.com/cdn/{0}/data/en_US/summoner.json";
    private const string SummonerSpellAssetUrl = "https://ddragon.leagueoflegends.com/cdn/{0}/img/spell/{1}";
    private const string RuneJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/perks.json";
    private const string RunePageJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/perkstyles.json";
    private const string ItemJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/items.json";
    private const string ItemAssetUrl = "https://raw.communitydragon.org/latest/game/assets/items/icons2d/";

    private static IEnumerable<PatchVersions> PatchVersions { get; set; } = [];
    
    private static MosgiContext CreateSeioriContext() => new(new DbContextOptionsBuilder<MosgiContext>().UseSqlServer(_connectionString).Options);

    public static async Task BeginAssetRoutine()
    {
        _wwwrootPath = Environment.GetEnvironmentVariable("WWWROOT_PATH") ?? throw new Exception("WWWROOT_PATH is not set");
        _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new Exception("CONNECTION_STRING is not set");
        
        var imagesDirectory = Path.Combine(_wwwrootPath, "images");
        
        if (!Directory.Exists(imagesDirectory))
        {
            Directory.CreateDirectory(imagesDirectory);
        }
        
        var championsDirectory = Path.Combine(_wwwrootPath, "images", "Champions");
        
        if (!Directory.Exists(championsDirectory))
        {
            Directory.CreateDirectory(championsDirectory);
        }
        
        await UpdatePatchVersionsAsync();

        await Task.WhenAll(
            UpdateChampionsAsync(),
            UpdateQueuesAsync(),
            UpdateProfileIconsAsync(),
            UpdateSummonerSpellsAsync(),
            UpdateRunesAsync(),
            UpdateItemsAsync()
        );
    }

    private static async Task UpdatePatchVersionsAsync()
    {
        try
        {
            await using var context = CreateSeioriContext();
            await using var stream = await HttpClient.GetStreamAsync(PatchVersionsUrl);
            using var doc = await JsonDocument.ParseAsync(stream);

            PatchVersions = doc.RootElement.GetProperty("patches").EnumerateArray()
                .Select(p => new PatchVersions
                {
                    PatchVersion = p.GetProperty("name").GetString() ?? string.Empty,
                    StartTimeStamp = p.GetProperty("start").GetInt64(),
                    IsLatest = false
                })
                .OrderByDescending(p => p.StartTimeStamp)
                .ToArray();
        
            if (!PatchVersions.Any()) throw new Exception("Patch Versions List is Empty");
            
            PatchVersions.FirstOrDefault()!.IsLatest = true;
            PatchVersions = PatchVersions
                .Take(PatchVersions.Count() - 3)
                .ToArray();

            await context.BulkInsertOrUpdateOrDeleteAsync(PatchVersions, options =>
            {
                options.PreserveInsertOrder = true;
                options.SetOutputIdentity = true;
            });
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Patch Versions", e);
        }
    }
    
    private static async Task UpdateChampionsAsync()
    {
        var championsList = new List<Champions>();
        var iconDirectories = new Dictionary<string, string>
        {
            { "Icon", Path.Combine(_wwwrootPath, "images", "Champions", "Icon") },
            { "Q", Path.Combine(_wwwrootPath, "images", "Champions", "Q") },
            { "W", Path.Combine(_wwwrootPath, "images", "Champions", "W") },
            { "E", Path.Combine(_wwwrootPath, "images", "Champions", "E") },
            { "R", Path.Combine(_wwwrootPath, "images", "Champions", "R") }
        };

        try
        {
            await using var context = CreateSeioriContext();
            
            // Ensure directories exist
            foreach (var directory in iconDirectories.Values.Where(directory => !Directory.Exists(directory)))
            {
                Directory.CreateDirectory(directory);
            }
            
            await using var stream = await HttpClient.GetStreamAsync(ChampionJsonUrl);
            using var doc = await JsonDocument.ParseAsync(stream);

            var championJsonList = doc.RootElement.EnumerateObject();

            while (championJsonList.MoveNext())
            {
                var champion = championJsonList.Current;
                var championId = champion.Value.GetProperty("id").GetInt16();
                var championName = champion.Value.GetProperty("name").GetString() ?? string.Empty;
                var patchLastUpdated = champion.Value.GetProperty("patchLastChanged").GetString() ?? string.Empty;

                var abilities = champion.Value.GetProperty("abilities");
                var imageUrls = new Dictionary<string, string>
                {
                    { "Icon", champion.Value.GetProperty("icon").GetString() ?? string.Empty },
                    { "Q", abilities.GetProperty("Q")[0].GetProperty("icon").GetString() ?? string.Empty },
                    { "W", abilities.GetProperty("W")[0].GetProperty("icon").GetString() ?? string.Empty },
                    { "E", abilities.GetProperty("E")[0].GetProperty("icon").GetString() ?? string.Empty },
                    { "R", abilities.GetProperty("R")[0].GetProperty("icon").GetString() ?? string.Empty }
                };

                // Process and save images
                foreach (var key in imageUrls.Keys)
                {
                    var imageUrl = imageUrls[key];
                    var directory = iconDirectories[key];
                    var fileName = $"{championId}.webp";
                    var filePath = Path.Combine(directory, fileName);

                    try
                    {
                        var imageBytes = await HttpClient.GetByteArrayAsync(imageUrl);
                        var webpBytes = ConvertImageToWebP(imageBytes);
                        await File.WriteAllBytesAsync(filePath, webpBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process {key} for Champion ID {championId}: {ex.Message}");
                    }
                }
                
                var patchVersionId = PatchVersions
                    .Where(p => p.PatchVersion == patchLastUpdated)
                    .Select(p => p.Id)
                    .FirstOrDefault();
                
                championsList.Add(new Champions
                {
                    Id = championId,
                    Name = championName,
                    PatchLastUpdated = patchVersionId
                });
            }

            if (championsList.Count == 0) throw new Exception("Champion List is Empty");

            await context.BulkInsertOrUpdateOrDeleteAsync(championsList);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Champions", e);
        }
    }

    private static async Task UpdateQueuesAsync()
    {
        var queueList = new List<Queues>();
        
        try
        {
            await using var context = CreateSeioriContext();
            await using var queueJsonStream = await HttpClient.GetStreamAsync(QueueJsonUrl);
            using var queueJsonDoc = await JsonDocument.ParseAsync(queueJsonStream);
            
            var queueJsonList = queueJsonDoc.RootElement.EnumerateArray();
            
            while (queueJsonList.MoveNext())
            {
                var queue = queueJsonList.Current;
                var queueId = queue.GetProperty("id").GetInt32();
                var queueName = queue.GetProperty("shortName").GetString() ?? string.Empty;
                
                queueList.Add(
                    new Queues()
                    {
                        Id = (short)queueId,
                        Name = queueName,
                    }
                );
            }
            
            if (queueList.Count == 0) throw new Exception("Queue List is Empty");   
            
            queueList = queueList
                .OrderBy(q => q.Id)
                .GroupBy(q => q.Id)
                .Select(q => q.First())
                .ToList();
            
            await context.BulkInsertOrUpdateOrDeleteAsync(queueList);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Queues", e);
        }
    }
    
    private static async Task UpdateProfileIconsAsync()
    {
        var profileIconsDirectory = Path.Combine(_wwwrootPath, "images", "ProfileIcons");

        try
        {
            await using var context = CreateSeioriContext();
            
            if (!Directory.Exists(profileIconsDirectory))
            {
                Directory.CreateDirectory(profileIconsDirectory);
            }
            
            await using var profileIconJsonStream = await HttpClient.GetStreamAsync(ProfileIconJsonUrl);
            using var profileIconJsonDoc = await JsonDocument.ParseAsync(profileIconJsonStream);

            var profileIconJsonList = profileIconJsonDoc.RootElement.EnumerateArray();

            while (profileIconJsonList.MoveNext())
            {
                var profileIcon = profileIconJsonList.Current;
                var profileIconId = profileIcon.GetProperty("id").GetInt16();

                string profileIconIconPath;
                if (profileIcon.TryGetProperty("iconPath", out var iconPathElement))
                {
                    profileIconIconPath = iconPathElement.GetString() ?? string.Empty;
                }
                else
                {
                    continue;
                }

                var profileIconImageUrl = GetAssetUrl(ProfileIconJsonUrl, profileIconIconPath);

                try
                {
                    var imageBytes = await HttpClient.GetByteArrayAsync(profileIconImageUrl);
                    var webpBytes = ConvertImageToWebP(imageBytes);
                    
                    var fileName = $"{profileIconId}.webp";
                    var filePath = Path.Combine(profileIconsDirectory, fileName);

                    await File.WriteAllBytesAsync(filePath, webpBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to process Profile Icon ID {profileIconId}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Profile Icons", e);
        }
    }
    
    private static async Task UpdateSummonerSpellsAsync()
    {
        var summonerSpellsDirectory = Path.Combine(_wwwrootPath, "images", "SummonerSpells");
        
        try
        {
            await using var context = CreateSeioriContext();
            
            if (!Directory.Exists(summonerSpellsDirectory))
            {
                Directory.CreateDirectory(summonerSpellsDirectory);
            }
            
            var latestPatchVersion = await context.PatchVersions
                .Where(p => p.IsLatest)
                .Select(p => p.PatchVersion)
                .FirstOrDefaultAsync();
            
            if (string.IsNullOrEmpty(latestPatchVersion)) throw new Exception("Latest Patch Version is Empty");
            
            await using var summonerSpellJsonStream = await HttpClient.GetStreamAsync(string.Format(SummonerSpellJsonUrl, $"{latestPatchVersion}.1"));
            using var summonerSpellJsonDoc = await JsonDocument.ParseAsync(summonerSpellJsonStream);
            
            var summonerSpellJsonList = summonerSpellJsonDoc.RootElement.GetProperty("data").EnumerateObject();
            
            while (summonerSpellJsonList.MoveNext())
            {
                var summonerSpell = summonerSpellJsonList.Current;
                var summonerSpellId = short.Parse(summonerSpell.Value.GetProperty("key").GetString() ?? string.Empty);
                var summonerSpellIconPath = summonerSpell.Value.GetProperty("image").GetProperty("full").GetString() ?? string.Empty;
                var summonerSpellImageUrl = string.Format(SummonerSpellAssetUrl, $"{latestPatchVersion}.1", summonerSpellIconPath);

                try
                {
                    var imageBytes = await HttpClient.GetByteArrayAsync(summonerSpellImageUrl);
                    var webpBytes = ConvertImageToWebP(imageBytes);
                
                    var fileName = $"{summonerSpellId}.webp";
                    var filePath = Path.Combine(summonerSpellsDirectory, fileName);
                
                    await File.WriteAllBytesAsync(filePath, webpBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to process Summoner Spell ID {summonerSpellId}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Summoner Spells", e);
        }
    }
    
    private static async Task UpdateRunesAsync()
    {
        var runesDirectory = Path.Combine(_wwwrootPath, "images", "Runes");
        
        try
        {
            if (!Directory.Exists(runesDirectory))
            {
                Directory.CreateDirectory(runesDirectory);
            }
            
            await using var runeJsonStream = await HttpClient.GetStreamAsync(RuneJsonUrl);
            using var runeJsonDoc = await JsonDocument.ParseAsync(runeJsonStream);
            
            var runeJsonList = runeJsonDoc.RootElement.EnumerateArray();
            
            while (runeJsonList.MoveNext())
            {
                var rune = runeJsonList.Current;
                var runeId = rune.GetProperty("id").GetInt16();
                var runeIconPath = rune.GetProperty("iconPath").GetString() ?? string.Empty;
                var runeImageUrl = GetAssetUrl(RuneJsonUrl, runeIconPath);

                try
                {
                    var imageBytes = await HttpClient.GetByteArrayAsync(runeImageUrl);
                    var webpBytes = ConvertImageToWebP(imageBytes);
                
                    var fileName = $"{runeId}.webp";
                    var filePath = Path.Combine(runesDirectory, fileName);
                
                    await File.WriteAllBytesAsync(filePath, webpBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to process Rune ID {runeId}: {e.Message}");
                }
            }
            
            await using var runePageJsonStream = await HttpClient.GetStreamAsync(RunePageJsonUrl);
            using var runePageJsonDoc = await JsonDocument.ParseAsync(runePageJsonStream);
            
            var runePageJsonList = runePageJsonDoc.RootElement.GetProperty("styles").EnumerateArray();
            
            while (runePageJsonList.MoveNext())
            {
                var runePage = runePageJsonList.Current;
                var runePageId = runePage.GetProperty("id").GetInt16();
                var runePageIconPath = runePage.GetProperty("iconPath").GetString() ?? string.Empty;
                var runePageImageUrl = GetAssetUrl(RunePageJsonUrl, runePageIconPath);

                try
                {
                    var imageBytes = await HttpClient.GetByteArrayAsync(runePageImageUrl);
                    var webpBytes = ConvertImageToWebP(imageBytes);
                
                    var fileName = $"{runePageId}.webp";
                    var filePath = Path.Combine(runesDirectory, fileName);
                
                    await File.WriteAllBytesAsync(filePath, webpBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to process Rune Page ID {runePageId}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Runes", e);
        }
    }
    
    private static async Task UpdateItemsAsync()
    {
        var itemsDirectory = Path.Combine(_wwwrootPath, "images", "Items");
        
        try
        {
            if (!Directory.Exists(itemsDirectory))
            {
                Directory.CreateDirectory(itemsDirectory);
            }
            
            await using var itemJsonStream = await HttpClient.GetStreamAsync(ItemJsonUrl);
            using var itemJsonDoc = await JsonDocument.ParseAsync(itemJsonStream);
            
            var itemJsonList = itemJsonDoc.RootElement.EnumerateArray();
            
            while (itemJsonList.MoveNext())
            {
                var item = itemJsonList.Current;
                var itemId = item.GetProperty("id").GetInt32();
                var itemIconPath = item.GetProperty("iconPath").GetString() ?? string.Empty;
                var itemImageUrl = $"{ItemAssetUrl}{itemIconPath.Split('/').Last().ToLower()}";;

                try
                {
                    var imageBytes = await HttpClient.GetByteArrayAsync(itemImageUrl);
                    var webpBytes = ConvertImageToWebP(imageBytes);
                
                    var fileName = $"{itemId}.webp";
                    var filePath = Path.Combine(itemsDirectory, fileName);
                
                    await File.WriteAllBytesAsync(filePath, webpBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to process Item ID {itemId}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to Update Items", e);
        }
    }
    
    private static string GetAssetUrl(string baseUrl, string assetPath)
    {
        var baseUrlIndex = baseUrl.IndexOf("v1/", StringComparison.Ordinal);
        var assetPathIndex = assetPath.IndexOf("v1/", StringComparison.Ordinal);
        
        return baseUrl[..baseUrlIndex] + assetPath[assetPathIndex..].ToLower();
    }
    
    private static byte[] ConvertImageToWebP(byte[] imageBytes)
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var image = Image.Load(inputStream);
        using var outputStream = new MemoryStream();
        
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(75, 75),
            Mode = ResizeMode.Crop
        }));
        
        var encoder = new WebpEncoder { Quality = 75 };
        image.Save(outputStream, encoder);

        return outputStream.ToArray();
    }
}