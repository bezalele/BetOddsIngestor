using Microsoft.EntityFrameworkCore;
using BetOddsIngestor.Data;
using BetOddsIngestor.Domain.Entities;
using BetOddsIngestor.Services;

namespace BetOddsIngestor.Services;

public class OddsIngestionService
{
    private readonly BettingDbContext _db;
    private readonly IOddsProviderClient _client;

    public OddsIngestionService(BettingDbContext db,IOddsProviderClient client){_db=db;_client=client;}

    public async Task RunOnceAsync()
    {
        var snaps=await _client.GetTodayOddsAsync();
        if(!snaps.Any()){Console.WriteLine("No odds returned");return;}

        // ensure sport + league
        var sport=await _db.Sports.FirstOrDefaultAsync(x=>x.Code=="BASKETBALL");
        if(sport==null){sport=new Sport{Name="Basketball",Code="BASKETBALL"};_db.Sports.Add(sport);await _db.SaveChangesAsync();}

        var league=await _db.Leagues.FirstOrDefaultAsync(x=>x.Code=="NBA");
        if(league==null){league=new League{Name="NBA",Code="NBA",SportId=sport.SportId};_db.Leagues.Add(league);await _db.SaveChangesAsync();}

        foreach(var s in snaps)
        {
            // provider
            var prov=await _db.OddsProviders.FirstOrDefaultAsync(x=>x.Code==s.ProviderCode);
            if(prov==null){prov=new OddsProvider{Name=s.ProviderCode,Code=s.ProviderCode};_db.OddsProviders.Add(prov);await _db.SaveChangesAsync();}

            // teams
            var ht=await FindTeam(league.LeagueId,s.HomeTeam);
            var at=await FindTeam(league.LeagueId,s.AwayTeam);

            // game dedup via ExternalRef
            var game=await _db.Games.FirstOrDefaultAsync(x=>x.ExternalRef==s.GameId);
            if(game==null){
                game=new Game{
                    LeagueId=league.LeagueId,
                    Season=s.GameTime.Year,
                    GameDateTime=s.GameTime,
                    HomeTeamId=ht.TeamId,
                    AwayTeamId=at.TeamId,
                    ExternalRef=s.GameId
                };
                _db.Games.Add(game);
                await _db.SaveChangesAsync();
            }

            var odds=new GameOdds{
                GameId=game.GameId,
                OddsProviderId=prov.OddsProviderId,
                SnapshotTimeUtc=DateTime.UtcNow,
                HomeMoneyline=s.HomeMoneyline,
                AwayMoneyline=s.AwayMoneyline,
                SpreadPoints=s.SpreadPoints,
                SpreadHomeOdds=s.SpreadHomeOdds,
                SpreadAwayOdds=s.SpreadAwayOdds,
                TotalPoints=s.TotalPoints,
                OverOdds=s.OverOdds,
                UnderOdds=s.UnderOdds
            };
            _db.GameOdds.Add(odds);
        }
        await _db.SaveChangesAsync();
        Console.WriteLine("Ingestion complete");
    }

    private async Task<Team> FindTeam(int leagueId,string name){
        var t=await _db.Teams.FirstOrDefaultAsync(x=>x.LeagueId==leagueId && x.Name==name);
        if(t!=null) return t;
        t=new Team{LeagueId=leagueId,Name=name};
        _db.Teams.Add(t);
        await _db.SaveChangesAsync();
        return t;
    }
}
