using Camille.RiotGames;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statikk_Scraper;
using Statikk_Scraper.Data;

var startTime = DateTime.Now;
Console.WriteLine($"Import Started At: {startTime}");

var rootPatch = Environment.GetEnvironmentVariable("WWWROOT_PATH") ?? throw new Exception("WWWROOT_PATH is not set");
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new Exception("CONNECTION_STRING is not set");
var riotApiKey = Environment.GetEnvironmentVariable("API_KEY") ?? throw new Exception("API_KEY is not set");

var imagesDirectory = Path.Combine(rootPatch, "images");
if (!Directory.Exists(imagesDirectory)) Directory.CreateDirectory(imagesDirectory);

var championsDirectory = Path.Combine(imagesDirectory, "Champions");
if (!Directory.Exists(championsDirectory)) Directory.CreateDirectory(championsDirectory);

var serviceProvider = new ServiceCollection()
    .AddDbContextFactory<Context>(options => options.UseSqlServer(connectionString))
    .AddHttpClient()
    .AddScoped<AssetRoutine>()
    .AddScoped<DataRoutine>()
    .AddSingleton<RiotGamesApi>(provider =>
    {
        var riotGamesApiConfig = new RiotGamesApiConfig.Builder(riotApiKey).Build();

        return RiotGamesApi.NewInstance(riotGamesApiConfig);
    })
    .BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var assetRoutine = scope.ServiceProvider.GetRequiredService<AssetRoutine>();
var dataRoutine = scope.ServiceProvider.GetRequiredService<DataRoutine>();

await assetRoutine.BeginAssetRoutine(rootPatch);
await dataRoutine.BeginDataRoutine();

var endTime = DateTime.Now;
Console.WriteLine($"Import Finished at: {endTime}");