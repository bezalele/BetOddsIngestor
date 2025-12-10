using Microsoft.EntityFrameworkCore;
using SmartSportsBetting.Infrastructure.Data;
using SmartSportsBetting.Domain.Entities;
using System.Globalization;

namespace BetOddsIngestor.Services;

public class OddsIngestionService
{
    private readonly BettingDbContext _db;
    private readonly IOddsProviderClient _client;

    public OddsIngestionService(BettingDbContext db, IOddsProviderClient client)
    {
        _db = db;
        _client = client;
    }

    public async Task RunOnceAsync()
    {
        var snaps = await _client.GetTodayOddsAsync();
        if (!snaps.Any())
        {
            Console.WriteLine("No odds returned");
            return;
        }

        // ensure sport + league
        var sport = await _db.Sports.FirstOrDefaultAsync(x => x.Code == "BASKETBALL");
        if (sport == null)
        {
            sport = new Sport { Name = "Basketball", Code = "BASKETBALL" };
            _db.Sports.Add(sport);
            await _db.SaveChangesAsync();
        }

        var league = await _db.Leagues.FirstOrDefaultAsync(x => x.Code == "NBA");
        if (league == null)
        {
            league = new League { Name = "NBA", Code = "NBA", SportId = sport.SportId };
            _db.Leagues.Add(league);
            await _db.SaveChangesAsync();
        }

        // Ensure MONEYLINE market type exists
        var mlType = await _db.MarketTypes.FirstOrDefaultAsync(m => m.Code == "MONEYLINE");
        if (mlType == null)
        {
            mlType = new MarketType { Code = "MONEYLINE", Description = "Moneyline market" };
            _db.MarketTypes.Add(mlType);
            await _db.SaveChangesAsync();
        }

        // NBA slate is based on US Eastern Time
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        foreach (var s in snaps)
        {
            // provider
            var prov = await _db.OddsProviders.FirstOrDefaultAsync(x => x.Code == s.ProviderCode);
            if (prov == null)
            {
                prov = new OddsProvider
                {
                    Name = s.ProviderCode,
                    Code = s.ProviderCode,
                    IsActive = true
                };
                _db.OddsProviders.Add(prov);
                await _db.SaveChangesAsync();
            }

            // teams
            var ht = await FindTeam(league.LeagueId, s.HomeTeam);
            var at = await FindTeam(league.LeagueId, s.AwayTeam);

            // ----- CORRECT GAME TIME + SLATE DATE HANDLING -----

            // s.GameTime is provider's UTC time
            var utcStart = DateTime.SpecifyKind(s.GameTime, DateTimeKind.Utc);

            // Convert to Eastern for slate date
            var startEt = TimeZoneInfo.ConvertTimeFromUtc(utcStart, eastern);
            var slateDateEt = startEt.Date;              // NBA "game day" (ET)
            var season = startEt.Year.ToString();        // season from local year

            // game dedup via ExternalRef
            var game = await _db.Games.FirstOrDefaultAsync(x => x.ExternalRef == s.GameId);
            if (game == null)
            {
                game = new Game
                {
                    LeagueId = league.LeagueId,
                    Season = season,
                    StartTimeUtc = utcStart,   // true UTC tip time
                    GameDateUtc = slateDateEt, // correct slate date (ET)
                    HomeTeamId = ht.TeamId,
                    AwayTeamId = at.TeamId,
                    ExternalRef = s.GameId
                };
                _db.Games.Add(game);
                await _db.SaveChangesAsync();
            }
            else
            {
                // Keep existing game but correct any bad dates/times
                bool changed = false;

                if (game.StartTimeUtc != utcStart)
                {
                    game.StartTimeUtc = utcStart;
                    changed = true;
                }

                if (game.GameDateUtc != slateDateEt)
                {
                    game.GameDateUtc = slateDateEt;
                    changed = true;
                }

                if (!string.Equals(game.Season, season, StringComparison.Ordinal))
                {
                    game.Season = season;
                    changed = true;
                }

                if (changed)
                    await _db.SaveChangesAsync();
            }

            // Ensure Market for this game / moneyline
            var market = await _db.Markets.FirstOrDefaultAsync(m =>
                m.GameId == game.GameId &&
                m.MarketTypeId == mlType.MarketTypeId &&
                m.Period == "FULL_GAME");

            if (market == null)
            {
                market = new Market
                {
                    GameId = game.GameId,
                    MarketTypeId = mlType.MarketTypeId,
                    Period = "FULL_GAME",
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow
                };
                _db.Markets.Add(market);
                await _db.SaveChangesAsync();
            }

            // Ensure HOME/AWAY MarketOutcome entries
            var homeOutcome = await _db.MarketOutcomes.FirstOrDefaultAsync(o =>
                o.MarketId == market.MarketId && o.OutcomeCode == "HOME");
            if (homeOutcome == null)
            {
                homeOutcome = new MarketOutcome
                {
                    MarketId = market.MarketId,
                    OutcomeCode = "HOME",
                    Description = "Home ML",
                    SortOrder = 1
                };
                _db.MarketOutcomes.Add(homeOutcome);
                await _db.SaveChangesAsync();
            }

            var awayOutcome = await _db.MarketOutcomes.FirstOrDefaultAsync(o =>
                o.MarketId == market.MarketId && o.OutcomeCode == "AWAY");
            if (awayOutcome == null)
            {
                awayOutcome = new MarketOutcome
                {
                    MarketId = market.MarketId,
                    OutcomeCode = "AWAY",
                    Description = "Away ML",
                    SortOrder = 2
                };
                _db.MarketOutcomes.Add(awayOutcome);
                await _db.SaveChangesAsync();
            }

            // Insert snapshots per side
            if (s.HomeMoneyline.HasValue)
            {
                var snap = new SmartSportsBetting.Domain.Entities.OddsSnapshot
                {
                    MarketOutcomeId = homeOutcome.MarketOutcomeId,
                    ProviderId = prov.ProviderId,
                    SnapshotTimeUtc = DateTime.UtcNow,
                    AmericanOdds = s.HomeMoneyline ?? 0,
                    DecimalOdds = null,
                    Source = s.ProviderCode
                };
                _db.OddsSnapshots.Add(snap);
            }

            if (s.AwayMoneyline.HasValue)
            {
                var snap = new SmartSportsBetting.Domain.Entities.OddsSnapshot
                {
                    MarketOutcomeId = awayOutcome.MarketOutcomeId,
                    ProviderId = prov.ProviderId,
                    SnapshotTimeUtc = DateTime.UtcNow,
                    AmericanOdds = s.AwayMoneyline ?? 0,
                    DecimalOdds = null,
                    Source = s.ProviderCode
                };
                _db.OddsSnapshots.Add(snap);
            }
        }

        await _db.SaveChangesAsync();
        Console.WriteLine("Ingestion complete");
    }

    private async Task<Team> FindTeam(int leagueId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required", nameof(name));

        var normalizedName = name.Trim();

        // Build a stable, non-empty abbreviation from the name
        var abbrev = BuildTeamAbbreviation(normalizedName);

        // Try match by League + Name OR League + Abbreviation
        var team = await _db.Teams
            .FirstOrDefaultAsync(t =>
                t.LeagueId == leagueId &&
                (t.Name == normalizedName || t.Abbreviation == abbrev));

        if (team != null)
            return team;

        // Not found → create new
        team = new Team
        {
            LeagueId = leagueId,
            Name = normalizedName,
            Abbreviation = abbrev,
            ExternalRef = normalizedName, // or provider team id if you have it
            IsActive = true
        };

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        return team;
    }

    private static string BuildTeamAbbreviation(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "TEAM";

        // Strip non-alphanumerics and upper-case
        var cleaned = new string(name
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();

        // If that gives something long like LOSANGELESLAKERS, cut to 10
        if (cleaned.Length >= 3)
            return cleaned.Length > 10 ? cleaned[..10] : cleaned;

        // Fallback: just use what we have
        return cleaned.Length > 0 ? cleaned : "TEAM";
    }
}
