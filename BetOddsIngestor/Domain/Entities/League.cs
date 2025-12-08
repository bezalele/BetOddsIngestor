namespace BetOddsIngestor.Domain.Entities;

public class League
{
    public int LeagueId { get; set; }
    public int SportId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}
