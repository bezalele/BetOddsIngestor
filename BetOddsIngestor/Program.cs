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

        // --- HttpClient for external APIs -----------------------------------
        services.AddHttpClient<TheOddsApiClient>();
        services.AddSingleton<IOddsProviderClient, TheOddsApiClient>(sp =>
            sp.GetRequiredService<TheOddsApiClient>());

        // Placeholder schedule provider – replace with real implementation later
        services.AddSingleton<IScheduleProviderClient, StubScheduleProviderClient>();

        // --- Ingestion services ---------------------------------------------
        services.AddTransient<ScheduleIngestionService>();
        services.AddTransient<OddsIngestionService>();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    // --- 1) Run schedule ingest first (ensures all Games exist) -------------
    var scheduleIngestor = services.GetRequiredService<ScheduleIngestionService>();
    await scheduleIngestor.RunOnceAsync();

    // --- 2) Run odds ingest (attaches odds to the games from schedule) ------
    var oddsIngestor = services.GetRequiredService<OddsIngestionService>();
    await oddsIngestor.RunOnceAsync();

    Console.WriteLine("BetOddsIngestor run complete.");
}
catch (Exception ex)
{
    var logger = services.GetService<ILoggerFactory>()
        ?.CreateLogger("Bootstrap");

    logger?.LogError(ex, "Error running ingestion");
}

// exit
