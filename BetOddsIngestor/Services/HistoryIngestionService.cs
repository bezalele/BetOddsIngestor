using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using BetOddsIngestor.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartSportsBetting.Infrastructure.Data;

namespace BetOddsIngestor.Services
{
    /// <summary>
    /// Backfills GameResult for past NBA games using balldontlie.
    /// It only writes results for games that already exist in betting.Game.
    /// </summary>
    public sealed class HistoryIngestionService
    {
        private readonly BettingDbContext _db;
        private readonly BalldontlieNbaClient _client;
        private readonly TimeZoneInfo _eastern;

        public HistoryIngestionService(
            BettingDbContext db,
            BalldontlieNbaClient client,
            IConfiguration configuration)
        {
            _db = db;
            _client = client;
            _eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        public async Task RunOnceAsync()
        {
            Console.WriteLine("[History] Starting balldontlie backfill…");

            // Backfill last 30 days as a start
            var nowUtc = DateTime.UtcNow;
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _eastern);

            var fromEt = nowEt.Date.AddDays(-30);
            var toEt = nowEt.Date.AddDays(1);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromEt, _eastern);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toEt, _eastern);

            var games = await _client.GetGamesWithScoresAsync(fromUtc, toUtc);
            if (games.Count == 0)
            {
                Console.WriteLine("[History] No historical games with scores found.");
                return;
            }

            var connString = _db.Database.GetDbConnection().ConnectionString;

            using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            foreach (var g in games)
            {
                if (!g.HomeScore.HasValue || !g.AwayScore.HasValue)
                    continue;

                // Convert start time to ET slate date (your Game.GameDateUtc convention)
                var utcStart = DateTime.SpecifyKind(g.StartTimeUtc, DateTimeKind.Utc);
                var startEt = TimeZoneInfo.ConvertTimeFromUtc(utcStart, _eastern);
                var gameDateEt = startEt.Date;

                using var cmd = new SqlCommand("betting.usp_UpsertGameResult", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@LeagueCode", g.LeagueCode);
                cmd.Parameters.AddWithValue("@Season", g.Season);
                cmd.Parameters.AddWithValue("@GameDateUtc", gameDateEt);
                cmd.Parameters.AddWithValue("@HomeTeamName", g.HomeTeamName);
                cmd.Parameters.AddWithValue("@AwayTeamName", g.AwayTeamName);
                cmd.Parameters.AddWithValue("@HomeScore", g.HomeScore.Value);
                cmd.Parameters.AddWithValue("@AwayScore", g.AwayScore.Value);
                cmd.Parameters.AddWithValue("@FinalStatus", g.Status ?? "Final");

                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[History] Error upserting result for {g.HomeTeamName} vs {g.AwayTeamName}: {ex.Message}");
                }
            }

            Console.WriteLine("[History] Balldontlie backfill complete.");
        }
    }
}
