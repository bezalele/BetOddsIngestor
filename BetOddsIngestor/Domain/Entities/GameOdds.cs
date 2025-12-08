namespace BetOddsIngestor.Domain.Entities;

public class GameOdds
{
    public long GameOddsId { get; set; }
    public long GameId { get; set; }
    public int OddsProviderId { get; set; }
    public DateTime SnapshotTimeUtc { get; set; }
    public int? HomeMoneyline { get; set; }
    public int? AwayMoneyline { get; set; }
    public decimal? SpreadPoints { get; set; }
    public int? SpreadHomeOdds { get; set; }
    public int? SpreadAwayOdds { get; set; }
    public decimal? TotalPoints { get; set; }
    public int? OverOdds { get; set; }
    public int? UnderOdds { get; set; }
    public bool IsClosingLine { get; set; }
}
