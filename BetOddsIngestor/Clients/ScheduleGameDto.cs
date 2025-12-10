using System;

namespace BetOddsIngestor.Clients
{
    /// <summary>
    /// Generic NBA schedule game DTO coming from your schedule provider.
    /// </summary>
    public sealed class ScheduleGameDto
    {
        public string ExternalGameId { get; set; } = default!; // provider game id
        public string LeagueCode { get; set; } = "NBA";
        public string Season { get; set; } = default!;         // e.g. "2024-2025"
        public string HomeTeamName { get; set; } = default!;
        public string AwayTeamName { get; set; } = default!;
        public DateTime StartTimeUtc { get; set; }             // must be UTC
    }
}
