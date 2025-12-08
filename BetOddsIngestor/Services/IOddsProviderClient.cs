namespace BetOddsIngestor.Services;

public record OddsSnapshot(
    string GameId,
    DateTime GameTime,
    string ProviderCode,
    string HomeTeam,
    string AwayTeam,
    int? HomeMoneyline,
    int? AwayMoneyline,
    decimal? SpreadPoints,
    int? SpreadHomeOdds,
    int? SpreadAwayOdds,
    decimal? TotalPoints,
    int? OverOdds,
    int? UnderOdds
);

public interface IOddsProviderClient
{
    Task<IReadOnlyList<OddsSnapshot>> GetTodayOddsAsync(CancellationToken ct=default);
}
