using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BetOddsIngestor.Providers.TheOddsApi;
using BetOddsIngestor.Services;
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
        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddDbContext<BettingDbContext>(options => options.UseSqlServer(conn));
        }
        else
        {
            services.AddDbContext<BettingDbContext>(options => options.UseInMemoryDatabase("dev"));
        }

        services.AddHttpClient<TheOddsApiClient>(client =>
        {
            // HttpClient configuration can be done via appsettings
        });
        services.AddSingleton<IOddsProviderClient, TheOddsApiClient>(sp => sp.GetRequiredService<TheOddsApiClient>());

        services.AddTransient<OddsIngestionService>();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var ingestor = services.GetRequiredService<OddsIngestionService>();
    await ingestor.RunOnceAsync();
}
catch (Exception ex)
{
    var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Bootstrap");
    logger?.LogError(ex, "Error running ingestion");
}

// exit
