using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BetOddsIngestor.Clients;
using Microsoft.Extensions.Configuration;

namespace BetOddsIngestor.Providers.TheOddsApi
{
    /// <summary>
    /// Reads completed games with scores from TheOddsAPI /scores endpoint.
    /// Implements IResultsProviderClient for the results ingestion pipeline.
    /// </summary>
    public sealed class TheOddsApiScoresClient : IResultsProviderClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _sportKey;

        public TheOddsApiScoresClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            var section = configuration.GetSection("TheOddsApi");

            _apiKey = section["ApiKey"];
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("TheOddsApi:ApiKey is not configured.");

            var baseUrl = section["BaseUrl"] ?? "https://api.the-odds-api.com/v4";
            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            _sportKey = section["Sport"] ?? "basketball_nba";
        }

        public async Task<IReadOnlyList<ScoreGameDto>> GetScoresAsync(DateTime fromUtc, DateTime toUtc)
        {
            // Pull recent scores; we filter by [fromUtc, toUtc) ourselves
            const int daysFrom = 3;
            var url = $"sports/{_sportKey}/scores?apiKey={_apiKey}&daysFrom={daysFrom}";

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[TheOddsApiScoresClient] Error {response.StatusCode}: {body}");
                return Array.Empty<ScoreGameDto>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var results = new List<ScoreGameDto>();

            using var doc = JsonDocument.Parse(json);
            foreach (var ev in doc.RootElement.EnumerateArray())
            {
                if (!ev.TryGetProperty("commence_time", out var commenceProp))
                    continue;

                if (!DateTime.TryParse(commenceProp.GetString(), out var commenceUtc))
                    continue;

                if (commenceUtc.Kind == DateTimeKind.Unspecified)
                    commenceUtc = DateTime.SpecifyKind(commenceUtc, DateTimeKind.Utc);

                if (commenceUtc < fromUtc || commenceUtc >= toUtc)
                    continue;

                var homeTeam = ev.GetProperty("home_team").GetString() ?? "";
                var awayTeam = ev.GetProperty("away_team").GetString() ?? "";

                if (!ev.TryGetProperty("scores", out var scoresProp) ||
                    scoresProp.ValueKind != JsonValueKind.Array)
                    continue;

                int? homeScore = null;
                int? awayScore = null;

                foreach (var s in scoresProp.EnumerateArray())
                {
                    var name = s.GetProperty("name").GetString() ?? "";
                    var scoreStr = s.GetProperty("score").GetString();

                    if (string.IsNullOrWhiteSpace(scoreStr))
                        continue;

                    if (!int.TryParse(scoreStr, out var parsed))
                        continue;

                    if (string.Equals(name, homeTeam, StringComparison.OrdinalIgnoreCase))
                        homeScore = parsed;
                    else if (string.Equals(name, awayTeam, StringComparison.OrdinalIgnoreCase))
                        awayScore = parsed;
                }

                if (homeScore == null || awayScore == null)
                    continue;

                var id = ev.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var completed = ev.TryGetProperty("completed", out var compProp) && compProp.GetBoolean();
                var season = commenceUtc.Year.ToString();

                results.Add(new ScoreGameDto
                {
                    ExternalGameId = id,
                    LeagueCode = "NBA",
                    Season = season,
                    HomeTeamName = homeTeam,
                    AwayTeamName = awayTeam,
                    StartTimeUtc = commenceUtc,
                    HomeScore = homeScore,
                    AwayScore = awayScore,
                    Status = completed ? "Final" : "Completed"
                });
            }

            Console.WriteLine($"[TheOddsApiScoresClient] Returned {results.Count} scored games.");
            return results;
        }
    }
}
