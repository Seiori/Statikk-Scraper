using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data;

public class Context(DbContextOptions<Context> options) : DbContext(options)
{
    public DbSet<Audits> Audits { get; set; }
    public DbSet<Champions> Champions { get; set; }
    public DbSet<Matches> Matches { get; set; }
    public DbSet<Teams> Teams { get; set; }
    public DbSet<Participants> Participants { get; set; }
    public DbSet<Patches> Patches { get; set; }
    public DbSet<Summoners> Summoners { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!System.Diagnostics.Debugger.IsAttached) return;
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.LogTo(Console.WriteLine);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Audits
        modelBuilder.Entity<Audits>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).ValueGeneratedOnAdd();

            entity.Property(a => a.Method).HasMaxLength(255);
            entity.Property(a => a.Timestamp).ValueGeneratedOnAdd();
        });
        
        // Champions
        modelBuilder.Entity<Champions>(entity =>
        {
            entity.HasKey(c => c.Id);
            
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.Property(c => c.Name).HasMaxLength(40);
        });
        
        // Matches
        modelBuilder.Entity<Matches>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasAlternateKey(m => new { m.Region, m.GameId });
            
            entity.Property(m => m.Id).ValueGeneratedOnAdd();
            entity.Property(m => m.Region).HasConversion<byte>();
            entity.Property(m => m.Tier).HasConversion<byte>();
            entity.Property(m => m.Division).HasConversion<byte>();

            entity
                .HasOne(m => m.Patch)
                .WithMany(p => p.Matches)
                .HasForeignKey(m => m.PatchesId);
        });
        
        // MatchTeams
        modelBuilder.Entity<Teams>(entity =>
        {
            entity.HasKey(mt => mt.Id);
            entity.HasAlternateKey(mt => new { mt.MatchesId, mt.TeamId });
            
            entity.Property(mt => mt.Id).ValueGeneratedOnAdd();
            
            entity
                .HasOne(mt => mt.Match)
                .WithMany(m => m.Teams)
                .HasForeignKey(mt => mt.MatchesId);
        });
        
        // Participants
        modelBuilder.Entity<Participants>(entity =>
        {
            entity.HasKey(p => new { p.SummonersId, p.TeamsId });
            
            entity.Property(p => p.Role).HasConversion<byte>();

            entity
                .HasOne(p => p.Summoner)
                .WithMany(s => s.Participants)
                .HasForeignKey(p => p.SummonersId);
            
            entity
                .HasOne(p => p.Team)
                .WithMany(mt => mt.Participants)
                .HasForeignKey(p => p.TeamsId);

            entity
                .HasOne(p => p.Champion)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.ChampionsId);
        });
        
        // Patches
        modelBuilder.Entity<Patches>(entity =>
        {
            entity.HasKey(pv => pv.Id);
            entity.HasAlternateKey(pv => pv.PatchVersion);
            
            entity.Property(pv => pv.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.PatchVersion).HasMaxLength(5);
        });
        
        // Queues
        modelBuilder.Entity<Queues>(entity =>
        {
            entity.HasKey(q => q.QueueId);

            entity.Property(q => q.QueueId).ValueGeneratedNever();
            entity.Property(q => q.Name).HasMaxLength(255);
            entity.Property(q => q.ShortName).HasMaxLength(255);
            entity.Property(q => q.Description).HasMaxLength(255);
        });
        
        // Summoner Ranks
        modelBuilder.Entity<SummonerRanks>(entity =>
        {
            entity.HasKey(s => new { s.SummonersId, s.Season, s.Queue, s.Date });
            
            entity.Property(s => s.Queue).HasConversion<ushort>();
            entity.Property(s => s.Tier).HasConversion<byte>();
            entity.Property(s => s.Division).HasConversion<byte>();
            
            entity
                .HasOne(s => s.Summoner)
                .WithMany(s => s.Ranks)
                .HasForeignKey(s => s.SummonersId);
        });
        
        // Summoners
        modelBuilder.Entity<Summoners>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasAlternateKey(s => s.Puuid);
            entity.HasAlternateKey(s => new { s.Region, s.SummonerId });
            
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.Puuid).HasMaxLength(78);
            entity.Property(s => s.Region).HasConversion<byte>();
            entity.Property(s => s.SummonerId).HasMaxLength(58);
            entity.Property(s => s.RiotId).HasMaxLength(40);
            entity.Property(s => s.LastUpdated).ValueGeneratedOnAddOrUpdate();
        });
        
        base.OnModelCreating(modelBuilder);
    }
}