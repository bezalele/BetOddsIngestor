using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using BetOddsIngestor.Services;

namespace BetOddsIngestor.Providers.TheOddsApi;

public class TheOddsApiClient : IOddsProviderClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public TheOddsApiClient(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public async Task<IReadOnlyList<OddsSnapshot>> GetTodayOddsAsync(CancellationToken ct = default)
    {
        var s = _cfg.GetSection("TheOddsApi");
        var url =
            $"{s["BaseUrl"]}/sports/{s["Sport"]}/odds" +
            $"?apiKey={s["ApiKey"]}&regions={s["Regions"]}&markets={s["Markets"]}&oddsFormat={s["OddsFormat"]}";

        var games = await _http.GetFromJsonAsync<List<OddsApiGame>>(url, ct) ?? new();

        var snaps = new List<OddsSnapshot>();

        foreach (var g in games)
        {
            // 🔴 OLD: var bk = g.bookmakers.FirstOrDefault();
            // 🔵 NEW: loop over ALL bookmakers (FanDuel, DraftKings, etc.)
            foreach (var bk in g.bookmakers)
            {
                if (bk == null) continue;

                int? hml = null, aml = null, sho = null, sao = null, oo = null, uo = null;
                decimal? sp = null, tp = null;

                foreach (var m in bk.markets)
                {
                    if (m.key == "h2h")
                    {
                        foreach (var o in m.outcomes)
                        {
                            if (o.name == g.home_team) hml = (int)o.price;
                            if (o.name == g.away_team) aml = (int)o.price;
                        }
                    }
                    else if (m.key == "spreads")
                    {
                        foreach (var o in m.outcomes)
                        {
                            if (o.name == g.home_team)
                            {
                                sp = o.point;
                                sho = (int)o.price;
                            }
                            if (o.name == g.away_team)
                            {
                                sao = (int)o.price;
                            }
                        }
                    }
                    else if (m.key == "totals")
                    {
                        foreach (var o in m.outcomes)
                        {
                            if (o.name == "Over")
                            {
                                tp = o.point;
                                oo = (int)o.price;
                            }
                            if (o.name == "Under")
                            {
                                uo = (int)o.price;
                            }
                        }
                    }
                }

                snaps.Add(new OddsSnapshot(
                    GameId: g.id,
                    GameTime: g.commence_time,
                    ProviderCode: bk.key.ToUpper(),
                    HomeTeam: g.home_team,
                    AwayTeam: g.away_team,
                    HomeMoneyline: hml,
                    AwayMoneyline: aml,
                    SpreadPoints: sp,
                    SpreadHomeOdds: sho,
                    SpreadAwayOdds: sao,
                    TotalPoints: tp,
                    OverOdds: oo,
                    UnderOdds: uo
                ));
            }
        }

        return snaps;
    }
}
