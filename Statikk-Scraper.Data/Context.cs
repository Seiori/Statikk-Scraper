using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data
{
    public class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Audit> Audit { get; init; }
        public DbSet<Champions> Champions { get; init; }
        public DbSet<Matches> Matches { get; init; }
        public DbSet<MatchTeams> MatchTeams { get; init; }
        public DbSet<Participants> Participants { get; init; }
        public DbSet<Patches> Patches { get; init; }
        public DbSet<Queues> Queues { get; init; }
        public DbSet<Summoners> Summoners { get; init; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!System.Diagnostics.Debugger.IsAttached) return;
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.LogTo(Console.WriteLine);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Audit
            modelBuilder.Entity<Audit>(entity =>
            {
                entity.HasKey(a => new { a.Method, a.Date }).IsClustered();
                
                entity.Property(a => a.Method).IsRequired().HasMaxLength(256);
                entity.Property(a => a.Input);
                entity.Property(a => a.Exception).IsRequired();
                entity.Property(a => a.StackTrace).IsRequired();
                entity.Property(a => a.Date).HasDefaultValueSql("GETDATE()").ValueGeneratedOnAdd();
            });
            
            // Champions
            modelBuilder.Entity<Champions>(entity =>
            {
                entity.HasKey(c => c.Id);
                
                entity.Property(c => c.Id).ValueGeneratedNever();
                entity.Property(c => c.Name).HasMaxLength(40).IsRequired();
            });
            
            // Matches
            modelBuilder.Entity<Matches>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.HasAlternateKey(m => new { m.Platform, m.GameId }).IsClustered();
                
                entity.Property(m => m.Id).ValueGeneratedOnAdd();
                entity.Property(m => m.Platform).HasConversion<short>();
                entity.Property(m => m.GameId).IsRequired();
                entity.Property(m => m.PatchesId);
                entity.Property(m => m.Queue).HasConversion<short>();
                entity.Property(m => m.Tier).HasConversion<byte>();
                entity.Property(m => m.Division).HasConversion<byte>();
                entity.Property(m => m.DatePlayed).IsRequired();
                entity.Property(m => m.TimePlayed).IsRequired();
                entity.Property(m => m.WinningTeam).HasConversion<short>();
            });
            
            // MatchTeams
            modelBuilder.Entity<MatchTeams>(entity =>
            {
                entity.HasKey(mt => new { mt.MatchesId, mt.Team });
                
                entity.Property(mt => mt.Team).HasConversion<short>();
            });
            
            // Participants
            modelBuilder.Entity<Participants>(entity =>
            {
                entity.HasKey(p => new { p.SummonersId, p.MatchesId });
                
                entity.Property(p => p.Team).HasConversion<short>();
                entity.Property(p => p.Role).HasConversion<byte>();
                entity.Property(p => p.Kda).HasColumnType("decimal(9,2)");
                entity.Property(p => p.CreepScorePerMinute).HasColumnType("decimal(9,2)");
            });
            
            // Patches
            modelBuilder.Entity<Patches>(entity =>
            {
                entity.HasKey(pv => pv.Id);
                entity.HasAlternateKey(pv => pv.PatchVersion);
                
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.Property(pv => pv.PatchVersion).HasMaxLength(5).IsRequired();
            });
            
            // Queues
            modelBuilder.Entity<Queues>(entity =>
            {
                entity.HasKey(q => q.Id);
                
                entity.Property(q => q.Id).ValueGeneratedNever();
                entity.Property(q => q.Name).IsRequired();
            });
            
            // SummonerRanks
            modelBuilder.Entity<SummonerRanks>(entity =>
            {
                entity.HasKey(sr => new { sr.SummonersId, sr.Queue }).IsClustered();
                
                entity.Property(sr => sr.Queue).HasConversion<short>();
                entity.Property(sr => sr.Tier).HasConversion<byte>();
                entity.Property(sr => sr.Division).HasConversion<byte>();
            });
            
            // Summoners
            modelBuilder.Entity<Summoners>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasAlternateKey(s => s.Puuid);
                entity.HasAlternateKey(s => new { s.SummonerId, s.Platform }).IsClustered();
                
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.Puuid).HasMaxLength(78).IsRequired();
                entity.Property(s => s.SummonerId).HasMaxLength(58).IsRequired();
                entity.Property(s => s.Platform).HasConversion<short>().IsRequired();
                entity.Property(s => s.RiotId).HasMaxLength(40).IsRequired();
                entity.Property(s => s.ProfileIconId).IsRequired();
                entity.Property(s => s.SummonerLevel).IsRequired();
                entity.Property(s => s.LastUpdated)
                    .HasDefaultValueSql("GETDATE()")
                    .IsRequired();
            });
            
            base.OnModelCreating(modelBuilder);
        }
    }
}