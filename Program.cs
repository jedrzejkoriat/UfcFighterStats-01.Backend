using UfcStatsAPI.Contracts;
using UfcStatsAPI.Services;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using UfcStatsAPI.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

builder.Services.AddQuartz(q =>
{
	var jobKey = new JobKey("UpdateStats");

	q.AddJob<MyJobService>(options => options.WithIdentity(jobKey));

	q.AddTrigger(options => options
	.ForJob(jobKey)
	.WithIdentity("TriggerAfterUFCEvent")
	.WithCronSchedule("0 0 7 ? * SUN *"));

	q.AddTrigger(opts => opts
	.ForJob(jobKey)
	.WithIdentity("TriggerAfterRankingUpdate")
	.WithCronSchedule("0 59 23 ? * MON *"));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

builder.Services.AddScoped<MyJobService>();
builder.Services.AddScoped<IScrapperService, ScrapperService>();

var app = builder.Build();

// Configure the HTTP request pipeline

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
