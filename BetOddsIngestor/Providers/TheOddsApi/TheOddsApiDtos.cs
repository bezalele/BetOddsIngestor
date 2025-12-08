namespace BetOddsIngestor.Providers.TheOddsApi;

public class OddsApiGame
{
    public string id { get; set; } = "";
    public DateTime commence_time { get; set; }
    public string home_team { get; set; } = "";
    public string away_team { get; set; } = "";
    public List<OddsApiBookmaker> bookmakers { get; set; } = new();
}

public class OddsApiBookmaker
{
    public string key { get; set; } = "";
    public List<OddsApiMarket> markets { get; set; } = new();
}

public class OddsApiMarket
{
    public string key { get; set; } = "";
    public List<OddsApiOutcome> outcomes { get; set; } = new();
}

public class OddsApiOutcome
{
    public string name { get; set; } = "";
    public decimal price { get; set; }
    public decimal? point { get; set; }
}
