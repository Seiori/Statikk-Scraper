using Statikk_Scraper;

var startTime = DateTime.Now;
Console.WriteLine("Import Started at: " + startTime);

//await AssetRoutine.BeginAssetRoutine();
await DataRoutine.BeginDataRoutine();

var endTime = DateTime.Now;

Console.WriteLine("Import Finished at: " + endTime);