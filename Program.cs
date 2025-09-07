using UfcStatsAPI.Contracts;
using UfcStatsAPI.Configuration;
using System.Text.Json;
using UfcStatsAPI.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJsonConfiguration();
builder.Services.AddHttpClient();
builder.Services.AddQuartzJobConfiugration();
builder.Services.AddLoggerConfiguration();
builder.Services.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

var api = app.MapGroup("/");

// GET: /api
app.MapGet("/api", async (ILogger<Program> logger) =>
{
    logger.LogInformation("UFC Stats requiested");

    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ufcfighterdata.json");
    var json = JsonSerializer.Deserialize<List<WeightClassModel>>(await File.ReadAllTextAsync(filePath));
    return Results.Ok(json);
});

// GET: /api/pulse
app.MapGet("/api/pulse", (ILogger<Program> logger) =>
{
    logger.LogInformation("Pulse requiested");
    return Results.Ok("PULSE");
});

// Helper method to check if scrapping works immediately
/*app.MapGet("/api/scrap", async (IScrapperService scrapperService, ILogger<Program> logger) =>
{
    logger.LogInformation("Scrap requiested");
    string json = await scrapperService.ScrapUFCRankedFighterAsync();
    string filePath = "ufcfighterdata.json";
    File.WriteAllText(filePath, json);

    return Results.Ok(json);
});*/

app.Run();