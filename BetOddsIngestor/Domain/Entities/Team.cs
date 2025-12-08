namespace BetOddsIngestor.Domain.Entities;

public class Team
{
    public int TeamId { get; set; }
    public int LeagueId { get; set; }
    public string Name { get; set; } = "";
}
