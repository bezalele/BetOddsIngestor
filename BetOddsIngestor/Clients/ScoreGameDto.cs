using System;

namespace BetOddsIngestor.Clients
{
    public sealed class ScoreGameDto
    {
        public string ExternalGameId { get; set; } = default!;
        public string LeagueCode { get; set; } = "NBA";
        public string Season { get; set; } = default!;
        public string HomeTeamName { get; set; } = default!;
        public string AwayTeamName { get; set; } = default!;
        public DateTime StartTimeUtc { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public string Status { get; set; } = "Final"; // Completed, Final, etc.
    }
}
