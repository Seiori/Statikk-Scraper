using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data;

public class Context(DbContextOptions<Context> options) : DbContext(options)
{
    public DbSet<Summoners> Summoners { get; init; }
    public DbSet<SummonerRanks> SummonerRanks { get; init; }
    public DbSet<Champions> Champions { get; init; }
    public DbSet<Patches> Patches { get; init; }
    public DbSet<Matches> Matches { get; init; }
    public DbSet<MatchTeams> MatchTeams { get; init; }
    public DbSet<Participants> Participants { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!System.Diagnostics.Debugger.IsAttached) return;
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.LogTo(Console.WriteLine);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Summoners
        modelBuilder.Entity<Summoners>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasAlternateKey(s => s.Puuid);
            entity.HasAlternateKey(s => new { s.Platform, s.SummonerId });

            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.Platform).HasConversion<ushort>();
            entity.Property(s => s.LastUpdated).ValueGeneratedOnAddOrUpdate();
        });
            
        // SummonerRanks
        modelBuilder.Entity<SummonerRanks>(entity =>
        {
            entity.HasKey(sr => new { sr.SummonersId, sr.Queue });

            entity.Property(sr => sr.Queue).HasConversion<ushort>();
            entity.Property(sr => sr.Tier).HasConversion<byte>();
            entity.Property(sr => sr.Division).HasConversion<byte>();
        });
            
        // Champions
        modelBuilder.Entity<Champions>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).ValueGeneratedNever();
        });

        // Patches
        modelBuilder.Entity<Patches>(entity =>
        {
            entity.HasKey(pv => pv.Id);
            entity.HasAlternateKey(pv => pv.PatchVersion);

            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(pv => pv.PatchVersion).HasMaxLength(5).IsRequired();
        });
            
        // Matches
        modelBuilder.Entity<Matches>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasAlternateKey(m => new { m.Platform, m.GameId });

            entity.Property(m => m.Id).ValueGeneratedOnAdd();
            entity.Property(m => m.Platform).HasConversion<ushort>();
            entity.Property(m => m.Queue).HasConversion<ushort>();
            entity.Property(m => m.Tier).HasConversion<byte>();
            entity.Property(m => m.Division).HasConversion<byte>();
            entity.Property(m => m.WinningTeam).HasConversion<ushort>();
        });

        // MatchTeams
        modelBuilder.Entity<MatchTeams>(entity =>
        {
            entity.HasKey(mt => new { mt.MatchesId, mt.Team });

            entity.Property(mt => mt.Team).HasConversion<ushort>();
        });

        // Participants
        modelBuilder.Entity<Participants>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasAlternateKey(p => new { p.MatchesId, p.SummonersId });

            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.Team).HasConversion<ushort>();
            entity.Property(p => p.Kda).HasPrecision(5, 2);
        });

        base.OnModelCreating(modelBuilder);
    }
}