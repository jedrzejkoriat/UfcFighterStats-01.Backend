using Quartz;
using UfcStatsAPI.Contracts;
using UfcStatsAPI.Services;

namespace UfcStatsAPI.Configuration;

public static class IServiceCollectionExtensions 
{
    public static IServiceCollection AddJsonConfiguration(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
            options.SerializerOptions.WriteIndented = true;
        });

        return services;
    }

    public static IServiceCollection AddQuartzJobConfiugration(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("UpdateStats");

            q.AddJob<MyJobService>(options => options.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("TriggerAfterRankingUpdate")
            .WithCronSchedule("0 0 0 ? * * *", x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"))));
        });
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        services.AddScoped<MyJobService>();

        return services;
    }

    public static IServiceCollection AddLoggerConfiguration(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddFile("Logs/app-{Date}.log");
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IScrapperService, ScrapperService>();
        services.AddScoped<IYoutubeService, YoutubeService>();
        services.AddScoped<IGoogleService, GoogleService>();
        services.AddScoped<IWikipediaService, WikipediaService>();
        services.AddScoped<ISherdogService, SherdogService>();

        return services;
    }
}
