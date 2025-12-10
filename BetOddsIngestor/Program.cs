using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BetOddsIngestor.Providers.TheOddsApi;
using BetOddsIngestor.Services;
using BetOddsIngestor.Clients;
using SmartSportsBetting.Infrastructure.Data;

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "live";

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;
        var conn = configuration.GetConnectionString("BettingDb");

        // DbContext
        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddDbContext<BettingDbContext>(options => options.UseSqlServer(conn));
        }
        else
        {
            services.AddDbContext<BettingDbContext>(options => options.UseInMemoryDatabase("dev"));
        }

        // HTTP clients
        services.AddHttpClient<IOddsProviderClient, TheOddsApiClient>();
        services.AddHttpClient<IScheduleProviderClient, TheOddsApiScheduleClient>();
        services.AddHttpClient<IResultsProviderClient, TheOddsApiScoresClient>();
        services.AddHttpClient<BalldontlieNbaClient>();

        // Services
        services.AddTransient<ScheduleIngestionService>();
        services.AddTransient<OddsIngestionService>();
        services.AddTransient<ResultsIngestionService>();
        services.AddTransient<HistoryIngestionService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
    })
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    if (mode == "backfill-history")
    {
        var history = services.GetRequiredService<HistoryIngestionService>();
        await history.RunOnceAsync();
    }
    else
    {
        // Normal live ingestion: schedule + odds + results
        var schedule = services.GetRequiredService<ScheduleIngestionService>();
        await schedule.RunOnceAsync();

        var odds = services.GetRequiredService<OddsIngestionService>();
        await odds.RunOnceAsync();

        var results = services.GetRequiredService<ResultsIngestionService>();
        await results.RunOnceAsync();
    }

    Console.WriteLine("BetOddsIngestor run complete.");
}
catch (Exception ex)
{
    var logger = services
        .GetService<ILoggerFactory>()
        ?.CreateLogger("Bootstrap");

    logger?.LogError(ex, "Error running ingestion");
}
