using UfcStatsAPI.Contracts;
using UfcStatsAPI.Services;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using UfcStatsAPI.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UfcStatsAPI.Model;

var builder = WebApplication.CreateBuilder(args);

// Configure json serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
    options.SerializerOptions.WriteIndented = true;
});

// Add http client
builder.Services.AddHttpClient();

// Add quartz job for updating stats every day
builder.Services.AddQuartz(q =>
{
	var jobKey = new JobKey("UpdateStats");

	q.AddJob<MyJobService>(options => options.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
    .ForJob(jobKey)
    .WithIdentity("TriggerAfterRankingUpdate")
    .WithCronSchedule("0 0 0 ? * * *", x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"))));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddScoped<MyJobService>();

// Add logger
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile("Logs/app-{Date}.log");

// Add services
builder.Services.AddScoped<IScrapperService, ScrapperService>();
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddScoped<IGoogleService, GoogleService>();
builder.Services.AddHttpClient<IScrapperService, ScrapperService>();

// Build the app
var app = builder.Build();

// Redirect http to https
app.UseHttpsRedirection();

var api = app.MapGroup("/");

// GET /
app.MapGet("/", async (ILogger<Program> logger) =>
{
    logger.LogInformation("UFC Stats requiested");

    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ufcfighterdata.json");
    var json = JsonSerializer.Deserialize<List<WeightClassModel>>(await File.ReadAllTextAsync(filePath));
    return Results.Ok(json);
});

// GET /pulse
app.MapGet("/pulse", (ILogger<Program> logger) =>
{
    logger.LogInformation("Pulse requiested");
    return Results.Ok("PULSE");
});

/*app.MapGet("scrap", async (IScrapperService scrapperService, ILogger<Program> logger) =>
{
    logger.LogInformation("Scrap requiested");
    await scrapperService.GetRankedFighterStatsAsync();
    return Results.Ok("Scrap done");
});*/

app.Run();