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
    public sealed class ResultsIngestionService
    {   
        private readonly BettingDbContext _db;
        private readonly IResultsProviderClient _client;
        private readonly IConfiguration _config;
        private readonly TimeZoneInfo _eastern;

        public ResultsIngestionService(
            BettingDbContext db,
            IResultsProviderClient client,
            IConfiguration config)
        {
            _db = db;
            _client = client;
            _config = config;
            _eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        public async Task RunOnceAsync()
        {
            Console.WriteLine("[Results] Starting results ingest…");

            // window: last 3 days ET → tomorrow ET
            var nowUtc = DateTime.UtcNow;
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _eastern);
            var fromEt = nowEt.Date.AddDays(-3);
            var toEt = nowEt.Date.AddDays(1);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromEt, _eastern);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toEt, _eastern);

            Console.WriteLine($"[Results] Requesting scores from {fromUtc:u} to {toUtc:u}…");

            var scores = await _client.GetScoresAsync(fromUtc, toUtc);

            Console.WriteLine($"[Results] Provider returned {scores.Count} scored games.");

            if (scores.Count == 0)
            {
                Console.WriteLine("[Results] No scores to ingest.");
                return;
            }

            // Read the original configured connection string (keeps password)
            var connString = _config.GetConnectionString("BettingDb")
                             ?? throw new InvalidOperationException("Connection string 'BettingDb' is not configured.");

            using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            foreach (var s in scores)
            {
                if (!s.HomeScore.HasValue || !s.AwayScore.HasValue)
                {
                    Console.WriteLine($"[Results] Skipping {s.HomeTeamName} vs {s.AwayTeamName} – missing scores.");
                    continue;
                }

                // Convert StartTimeUtc → ET slate date, to match Game.GameDateUtc convention
                var utcStart = DateTime.SpecifyKind(s.StartTimeUtc, DateTimeKind.Utc);
                var startEt = TimeZoneInfo.ConvertTimeFromUtc(utcStart, _eastern);
                var gameDateUtc = startEt.Date;

                Console.WriteLine(
                    $"[Results] Upserting: {s.HomeTeamName} vs {s.AwayTeamName} " +
                    $"on {gameDateUtc:yyyy-MM-dd} (Season {s.Season}), " +
                    $"Score {s.HomeScore}:{s.AwayScore}");

                using var cmd = new SqlCommand("betting.usp_UpsertGameResult", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@LeagueCode", s.LeagueCode);
                cmd.Parameters.AddWithValue("@Season", (object?)s.Season ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@GameDateUtc", gameDateUtc);
                cmd.Parameters.AddWithValue("@HomeTeamName", s.HomeTeamName);
                cmd.Parameters.AddWithValue("@AwayTeamName", s.AwayTeamName);
                cmd.Parameters.AddWithValue("@HomeScore", s.HomeScore.Value);
                cmd.Parameters.AddWithValue("@AwayScore", s.AwayScore.Value);
                cmd.Parameters.AddWithValue("@FinalStatus", (object?)s.Status ?? "Final");

                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Results] ERROR calling usp_UpsertGameResult for {s.HomeTeamName} vs {s.AwayTeamName}: {ex.Message}");
                }
            }

            Console.WriteLine("[Results] Results ingest complete.");
        }
    }
}
