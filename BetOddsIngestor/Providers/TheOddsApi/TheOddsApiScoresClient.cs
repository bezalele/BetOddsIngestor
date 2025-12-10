using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BetOddsIngestor.Clients;
using Microsoft.Extensions.Configuration;

namespace BetOddsIngestor.Providers.TheOddsApi
{
    public sealed class TheOddsApiScoresClient : IResultsProviderClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _sportKey;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            // Pull recent scores (last few days), then filter by [fromUtc, toUtc)
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
            var events = JsonSerializer.Deserialize<List<ScoreEventDto>>(json, JsonOptions)
                         ?? new List<ScoreEventDto>();

            var results = new List<ScoreGameDto>();

            foreach (var e in events)
            {
                if (e.CommenceTime == null || e.Scores == null || e.Scores.Count == 0)
                    continue;

                var commenceUtc = e.CommenceTime.Value;
                if (commenceUtc.Kind == DateTimeKind.Unspecified)
                    commenceUtc = DateTime.SpecifyKind(commenceUtc, DateTimeKind.Utc);

                // filter by [fromUtc, toUtc)
                if (commenceUtc < fromUtc || commenceUtc >= toUtc)
                    continue;

                int? homeScore = null;
                int? awayScore = null;

                foreach (var s in e.Scores)
                {
                    if (!int.TryParse(s.Score, out var parsed))
                        continue;

                    if (string.Equals(s.Name, e.HomeTeam, StringComparison.OrdinalIgnoreCase))
                        homeScore = parsed;
                    else if (string.Equals(s.Name, e.AwayTeam, StringComparison.OrdinalIgnoreCase))
                        awayScore = parsed;
                }

                if (homeScore == null || awayScore == null)
                    continue;

                var season = commenceUtc.Year.ToString();

                results.Add(new ScoreGameDto
                {
                    ExternalGameId = e.Id,
                    LeagueCode = "NBA",
                    Season = season,
                    HomeTeamName = e.HomeTeam ?? string.Empty,
                    AwayTeamName = e.AwayTeam ?? string.Empty,
                    StartTimeUtc = commenceUtc,
                    HomeScore = homeScore,
                    AwayScore = awayScore,
                    Status = e.Completed ? "Final" : "Completed"
                });
            }

            Console.WriteLine($"[TheOddsApiScoresClient] Returned {results.Count} scored games.");
            return results;
        }

        // DTOs that match TheOddsAPI scores JSON
        private sealed class ScoreEventDto
        {
            public string Id { get; set; } = default!;
            public DateTime? CommenceTime { get; set; }
            public string HomeTeam { get; set; } = default!;
            public string AwayTeam { get; set; } = default!;
            public bool Completed { get; set; }
            public List<ScoreDto>? Scores { get; set; }
        }

        private sealed class ScoreDto
        {
            public string Name { get; set; } = default!;
            public string Score { get; set; } = default!; // string in API, we parse
        }
    }
}
