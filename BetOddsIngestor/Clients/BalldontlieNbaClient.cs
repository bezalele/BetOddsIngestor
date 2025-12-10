using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BetOddsIngestor.Clients
{
    /// <summary>
    /// Pulls historical NBA schedule + results from balldontlie.
    /// Maps them into ScoreGameDto for the results pipeline.
    /// </summary>
    public sealed class BalldontlieNbaClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public BalldontlieNbaClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            var section = configuration.GetSection("Balldontlie");
            _baseUrl = section["BaseUrl"] ?? "https://api.balldontlie.io/v1";

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/");

            // Optional API key header
            var apiKey = section["ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                _httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);
        }

        public async Task<IReadOnlyList<ScoreGameDto>> GetGamesWithScoresAsync(DateTime fromUtc, DateTime toUtc)
        {
            var results = new List<ScoreGameDto>();

            var startDate = fromUtc.ToString("yyyy-MM-dd");
            var endDate = toUtc.ToString("yyyy-MM-dd");

            int page = 1;
            const int perPage = 100;

            while (true)
            {
                var url = $"games?start_date={startDate}&end_date={endDate}&per_page={perPage}&page={page}";

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"[Balldontlie] Error {response.StatusCode}: {body}");
                    break;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                    break;

                foreach (var g in data.EnumerateArray())
                {
                    // date → UTC
                    if (!g.TryGetProperty("date", out var dateProp))
                        continue;

                    if (!DateTime.TryParse(dateProp.GetString(), out var dt))
                        continue;

                    if (dt.Kind == DateTimeKind.Unspecified)
                        dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                    if (dt < fromUtc || dt >= toUtc)
                        continue;

                    // teams
                    var homeTeam = g.GetProperty("home_team").GetProperty("full_name").GetString() ?? "";
                    var awayTeam = g.GetProperty("visitor_team").GetProperty("full_name").GetString() ?? "";

                    var homeScore = g.GetProperty("home_team_score").GetInt32();
                    var awayScore = g.GetProperty("visitor_team_score").GetInt32();

                    // skip scheduled/no-score games
                    if (homeScore == 0 && awayScore == 0)
                        continue;

                    var season = g.GetProperty("season").GetInt32().ToString();
                    var id = g.GetProperty("id").GetInt32().ToString();

                    results.Add(new ScoreGameDto
                    {
                        ExternalGameId = id,
                        LeagueCode = "NBA",
                        Season = season,
                        HomeTeamName = homeTeam,
                        AwayTeamName = awayTeam,
                        StartTimeUtc = dt,
                        HomeScore = homeScore,
                        AwayScore = awayScore,
                        Status = "Final"
                    });
                }

                // pagination
                if (!doc.RootElement.TryGetProperty("meta", out var meta) ||
                    !meta.TryGetProperty("next_page", out var nextPageProp) ||
                    nextPageProp.ValueKind == JsonValueKind.Null)
                {
                    break;
                }

                var nextPg = nextPageProp.GetInt32();
                if (nextPg <= page)
                    break;

                page = nextPg;
            }

            Console.WriteLine($"[Balldontlie] Returned {results.Count} games with scores.");
            return results;
        }
    }
}
