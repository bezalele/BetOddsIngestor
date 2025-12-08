using Microsoft.EntityFrameworkCore;
using BetOddsIngestor.Domain.Entities;

namespace BetOddsIngestor.Data;

public class BettingDbContext : DbContext
{
    public BettingDbContext(DbContextOptions<BettingDbContext> options) : base(options) {}

    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameOdds> GameOdds => Set<GameOdds>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<OddsProvider> OddsProviders => Set<OddsProvider>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Game>().ToTable("Game","betting").HasKey(x=>x.GameId);
        b.Entity<GameOdds>().ToTable("GameOdds","betting").HasKey(x=>x.GameOddsId);
        b.Entity<Team>().ToTable("Team","betting").HasKey(x=>x.TeamId);
        b.Entity<League>().ToTable("League","betting").HasKey(x=>x.LeagueId);
        b.Entity<Sport>().ToTable("Sport","betting").HasKey(x=>x.SportId);
        b.Entity<OddsProvider>().ToTable("OddsProvider","betting").HasKey(x=>x.OddsProviderId);
    }
}
