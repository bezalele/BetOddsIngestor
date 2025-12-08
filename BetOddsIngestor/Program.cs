using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BetOddsIngestor.Data;
using BetOddsIngestor.Services;
using BetOddsIngestor.Providers.TheOddsApi;

var b=Host.CreateApplicationBuilder(args);
b.Configuration.AddJsonFile("appsettings.json",false,true);

b.Services.AddDbContext<BettingDbContext>(o=>o.UseSqlServer(b.Configuration.GetConnectionString("BettingDb")));
b.Services.AddHttpClient<TheOddsApiClient>();
b.Services.AddScoped<IOddsProviderClient>(sp=>sp.GetRequiredService<TheOddsApiClient>());
b.Services.AddScoped<OddsIngestionService>();

var h=b.Build();
using var scope=h.Services.CreateScope();
var svc=scope.ServiceProvider.GetRequiredService<OddsIngestionService>();
await svc.RunOnceAsync();
Console.WriteLine("Done");
