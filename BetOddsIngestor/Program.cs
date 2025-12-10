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

        // --- DbContext -------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddDbContext<BettingDbContext>(options => options.UseSqlServer(conn));
        }
        else
        {
            services.AddDbContext<BettingDbContext>(options => options.UseInMemoryDatabase("dev"));
        }

        // --- HttpClients for external APIs ----------------------------------
        services.AddHttpClient<TheOddsApiClient>();
        services.AddSingleton<IOddsProviderClient, TheOddsApiClient>(sp =>
            sp.GetRequiredService<TheOddsApiClient>());

        services.AddHttpClient<TheOddsApiScheduleClient>();
        services.AddSingleton<IScheduleProviderClient, TheOddsApiScheduleClient>(sp =>
            sp.GetRequiredService<TheOddsApiScheduleClient>());

        services.AddHttpClient<TheOddsApiScoresClient>();
        services.AddSingleton<IResultsProviderClient, TheOddsApiScoresClient>(sp =>
            sp.GetRequiredService<TheOddsApiScoresClient>());

        // --- Ingestion services ---------------------------------------------
        services.AddTransient<ScheduleIngestionService>();
        services.AddTransient<OddsIngestionService>();
        services.AddTransient<ResultsIngestionService>();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    // 1) Schedule ingest
    var scheduleIngestor = services.GetRequiredService<ScheduleIngestionService>();
    await scheduleIngestor.RunOnceAsync();

    // 2) Odds ingest
    var oddsIngestor = services.GetRequiredService<OddsIngestionService>();
    await oddsIngestor.RunOnceAsync();

    // 3) Results ingest (for completed games)
    var resultsIngestor = services.GetRequiredService<ResultsIngestionService>();
    await resultsIngestor.RunOnceAsync();

    Console.WriteLine("BetOddsIngestor run complete.");
}
catch (Exception ex)
{
    var logger = services.GetService<ILoggerFactory>()
        ?.CreateLogger("Bootstrap");

    logger?.LogError(ex, "Error running ingestion");
}
