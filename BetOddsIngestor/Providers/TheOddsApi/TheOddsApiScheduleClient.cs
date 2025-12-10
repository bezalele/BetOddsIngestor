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
    /// Schedule provider using The Odds API v4.
    /// We use the odds endpoint just to get event id, teams and commence_time as schedule.
    /// </summary>
    public sealed class TheOddsApiScheduleClient : IScheduleProviderClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _region;
        private readonly string _sportKey;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TheOddsApiScheduleClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            // Read the SAME section/keys as your appsettings.json
            var section = configuration.GetSection("TheOddsApi");

            _apiKey = section["ApiKey"];
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("TheOddsApi:ApiKey is not configured (check appsettings.json).");
            }

            // BaseUrl: "https://api.the-odds-api.com/v4"
            var baseUrl = section["BaseUrl"] ?? "https://api.the-odds-api.com/v4";
            if (!_httpClient.BaseAddress?.ToString().StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase) ?? true)
            {
                _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }

            // Sport & region from your config
            _sportKey = section["Sport"] ?? "basketball_nba"; // e.g. basketball_nba
            _region = section["Regions"] ?? "us";           // e.g. us
        }

        public async Task<IReadOnlyList<ScheduleGameDto>> GetScheduleAsync(DateTime fromUtc, DateTime toUtc)
        {
            // Use odds endpoint to get all upcoming NBA events
            var url = $"sports/{_sportKey}/odds" +
                      $"?apiKey={_apiKey}" +
                      $"&regions={_region}" +
                      $"&markets=h2h" +
                      $"&oddsFormat=american";

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[TheOddsApiScheduleClient] Error {response.StatusCode}: {body}");
                return Array.Empty<ScheduleGameDto>();
            }

            var json = await response.Content.ReadAsStringAsync();

            var events = JsonSerializer.Deserialize<List<TheOddsApiEventDto>>(json, JsonOptions)
                         ?? new List<TheOddsApiEventDto>();

            var result = new List<ScheduleGameDto>();

            foreach (var e in events)
            {
                if (e.CommenceTime == null)
                    continue;

                var commenceUtc = e.CommenceTime.Value;
                if (commenceUtc.Kind == DateTimeKind.Unspecified)
                {
                    commenceUtc = DateTime.SpecifyKind(commenceUtc, DateTimeKind.Utc);
                }

                // Filter by requested window
                if (commenceUtc < fromUtc || commenceUtc >= toUtc)
                    continue;

                var season = commenceUtc.Year.ToString();

                result.Add(new ScheduleGameDto
                {
                    ExternalGameId = e.Id,
                    LeagueCode = "NBA",
                    Season = season,
                    HomeTeamName = e.HomeTeam ?? string.Empty,
                    AwayTeamName = e.AwayTeam ?? string.Empty,
                    StartTimeUtc = commenceUtc
                });
            }

            Console.WriteLine($"[TheOddsApiScheduleClient] Returned {result.Count} games between {fromUtc:u} and {toUtc:u}.");
            return result;
        }

        // --- Internal DTO for The Odds API response -------------------------

        private sealed class TheOddsApiEventDto
        {
            public string Id { get; set; } = default!;
            public string Sport_Key { get; set; } = default!;
            public string Sport_Title { get; set; } = default!;
            public DateTime? CommenceTime { get; set; }  // maps commence_time
            public string HomeTeam { get; set; } = default!;
            public string AwayTeam { get; set; } = default!;
        }
    }
}
