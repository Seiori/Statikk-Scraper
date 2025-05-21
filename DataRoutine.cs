using System.Text.Json;
using Seiori.MySql;
using Seiori.MySql.Enums;
using Statikk_Scraper.Data;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Statikk_Scraper;

public class DataRoutine(Context context, IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private const string PatchVersionsUrl = "https://cdn.merakianalytics.com/riot/lol/resources/patches.json";
    private const string QueueJsonUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/queues.json";
    private const string ChampionJsonUrl = "https://cdn.merakianalytics.com/riot/lol/resources/latest/en-US/champions.json";
    
    public async Task BeginDataRoutine()
    {
        try
        {
            await UpdatePatchVersionsAsync();
            await UpdateQueuesAsync();
            await UpdateChampionsAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private async Task UpdatePatchVersionsAsync()
    {
        await using var stream = await _httpClient.GetStreamAsync(PatchVersionsUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        var patches = doc.RootElement.GetProperty("patches")
            .EnumerateArray()
            .Select(p => new Patches
            {
                Season = byte.TryParse((p.GetProperty("name").GetString() ?? string.Empty).Split('.').First(), out var season) ? season : byte.MinValue,
                PatchVersion = p.GetProperty("name").GetString() ?? string.Empty,
            })
            .Where(p => p.PatchVersion.Length <= 5)
            .DistinctBy(p => p.PatchVersion)
            .ToArray();

        if (patches.Length is 0) throw new Exception("Patch Versions List is Empty");
        await context.BulkOperationAsync(BulkOperation.Insert, patches);
    }

    private async Task UpdateQueuesAsync()
    {
        await using var stream = await _httpClient.GetStreamAsync(QueueJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        var queues = doc.RootElement
            .EnumerateArray()
            .Select(q => new Queues()
            {
                QueueId = (ushort)q.GetProperty("id").GetInt16(),
                Name = q.GetProperty("name").GetString() ?? string.Empty,
                ShortName = q.GetProperty("shortName").GetString() ?? string.Empty,
                Description = q.GetProperty("description").GetString() ?? string.Empty,
            })
            .ToArray();
        
        if (queues.Length is 0) throw new Exception("Queue List is Empty");
        await context.BulkOperationAsync(BulkOperation.Insert, queues);
    }

    private async Task UpdateChampionsAsync()
    {
        await using var stream = await _httpClient.GetStreamAsync(ChampionJsonUrl);
        using var doc = await JsonDocument.ParseAsync(stream);
        var championArray = doc.RootElement
            .EnumerateObject()
            .Select(c => new Champions()
            {
                Id = (ushort)c.Value.GetProperty("id").GetInt16(),
                Name = c.Value.GetProperty("name").GetString() ?? string.Empty,
            })
            .ToArray();
        
        if (championArray.Length is 0) throw new Exception("Champion List is Empty");
        if (championArray.Any(c => c.Id <= 0)) throw new Exception("Champion Id is Invalid");
        if (championArray.Any(c => string.IsNullOrEmpty(c.Name))) throw new Exception("Champion Name is Invalid");
        await context.BulkOperationAsync(BulkOperation.Insert, championArray);
    }
}