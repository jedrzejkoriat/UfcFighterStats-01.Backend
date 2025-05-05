using UfcStatsAPI.Contracts;
using UfcStatsAPI.Services;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using UfcStatsAPI.Configuration;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

builder.Services.AddHttpClient();

builder.Services.AddQuartz(q =>
{
	var jobKey = new JobKey("UpdateStats");

	q.AddJob<MyJobService>(options => options.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
    .ForJob(jobKey)
    .WithIdentity("TriggerAfterRankingUpdate")
    .WithCronSchedule("0 59 23 ? * WED *"));
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile("Logs/app-{Date}.log");

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddScoped<MyJobService>();
builder.Services.AddScoped<IScrapperService, ScrapperService>();
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddScoped<IGoogleService, GoogleService>();

builder.Services.AddHttpClient<IScrapperService, ScrapperService>();

var app = builder.Build();

// Configure the HTTP request pipeline

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
