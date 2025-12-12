using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BetOddsIngestor;
using BetOddsIngestor.Clients;
using Microsoft.EntityFrameworkCore;
using SmartSportsBetting.Domain.Entities;
using SmartSportsBetting.Infrastructure.Data;

namespace BetOddsIngestor.Services
{
    /// <summary>
    /// Ensures every NBA game in a date window exists in betting.Game.
    /// Uses Eastern Time to derive GameDateUtc (slate day).
    /// </summary>
    public sealed class ScheduleIngestionService
    {
        private readonly BettingDbContext _db;
        private readonly IScheduleProviderClient _client;
        public ScheduleIngestionService(BettingDbContext db, IScheduleProviderClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task RunOnceAsync()
        {
            Console.WriteLine("[Schedule] Starting NBA schedule ingest…");

            // window: yesterday ET -> +7 days ET (safe buffer)
            var nowUtc = DateTime.UtcNow;
            var nowEt = EasternTime.ConvertFromUtc(nowUtc);

            var fromEt = nowEt.Date.AddDays(-1);
            var toEt = nowEt.Date.AddDays(7);

            var fromUtc = EasternTime.ConvertToUtc(fromEt);
            var toUtc = EasternTime.ConvertToUtc(toEt);

            var games = await _client.GetScheduleAsync(fromUtc, toUtc);
            if (!games.Any())
            {
                Console.WriteLine("[Schedule] No games returned from schedule provider.");
                return;
            }

            // Ensure sport + league (same as OddsIngestionService)
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
                league = new League
                {
                    Name = "NBA",
                    Code = "NBA",
                    SportId = sport.SportId
                };
                _db.Leagues.Add(league);
                await _db.SaveChangesAsync();
            }

            int upsertCount = 0;

            foreach (var g in games)
            {
                // Teams (reuse same naming logic as odds ingestion)
                var homeTeam = await FindOrCreateTeamAsync(league.LeagueId, g.HomeTeamName);
                var awayTeam = await FindOrCreateTeamAsync(league.LeagueId, g.AwayTeamName);

                // Normalize to UTC and ET slate date
                var startUtc = DateTime.SpecifyKind(g.StartTimeUtc, DateTimeKind.Utc);
                var startEt = EasternTime.ConvertFromUtc(startUtc);
                var gameDateUtc = startEt.Date; // slate day in Eastern

                // Match by ExternalRef (same field odds ingestion uses)
                var game = await _db.Games
                    .FirstOrDefaultAsync(x => x.ExternalRef == g.ExternalGameId);

                if (game == null)
                {
                    game = new Game
                    {
                        LeagueId = league.LeagueId,
                        Season = g.Season,
                        StartTimeUtc = startUtc,
                        GameDateUtc = gameDateUtc,
                        HomeTeamId = homeTeam.TeamId,
                        AwayTeamId = awayTeam.TeamId,
                        ExternalRef = g.ExternalGameId
                    };

                    _db.Games.Add(game);
                }
                else
                {
                    // Update in case time / teams changed
                    game.StartTimeUtc = startUtc;
                    game.GameDateUtc = gameDateUtc;
                    game.HomeTeamId = homeTeam.TeamId;
                    game.AwayTeamId = awayTeam.TeamId;

                    if (!string.IsNullOrWhiteSpace(g.Season))
                        game.Season = g.Season;
                }

                upsertCount++;
            }

            await _db.SaveChangesAsync();
            Console.WriteLine($"[Schedule] Schedule ingest complete. Upserted {upsertCount} games.");
        }

        // ---- helpers (copied from OddsIngestionService to keep behavior identical) ----

        private async Task<Team> FindOrCreateTeamAsync(int leagueId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Team name is required", nameof(name));

            var normalizedName = name.Trim();
            var abbrev = BuildTeamAbbreviation(normalizedName);

            var team = await _db.Teams
                .FirstOrDefaultAsync(t =>
                    t.LeagueId == leagueId &&
                    (t.Name == normalizedName || t.Abbreviation == abbrev));

            if (team != null)
                return team;

            team = new Team
            {
                LeagueId = leagueId,
                Name = normalizedName,
                Abbreviation = abbrev,
                ExternalRef = normalizedName,
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

            var cleaned = new string(name
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToUpperInvariant();

            if (cleaned.Length >= 3)
                return cleaned.Length > 10 ? cleaned[..10] : cleaned;

            return cleaned.Length > 0 ? cleaned : "TEAM";
        }
    }
}
