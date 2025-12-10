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
        // Default builder already sets content root to the project folder
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
    // 1) Run schedule ingest first (ensures all Games exist)
    var scheduleIngestor = services.GetRequiredService<ScheduleIngestionService>();
    await scheduleIngestor.RunOnceAsync();

    // 2) Run odds ingest (attaches odds to those games)
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
