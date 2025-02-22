using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Models;

namespace Statikk_Scraper.Data
{
    public class MosgiContext(DbContextOptions<MosgiContext> options) : DbContext(options)
    {
        public DbSet<Audit> Audit { get; init; }
        public DbSet<PatchVersions> PatchVersions { get; init; }
        public DbSet<Summoners> Summoners { get; init; }
        public DbSet<Champions> Champions { get; init; }
        public DbSet<ChampionPages> ChampionPages { get; init; }

        public DbSet<Queues> Queues { get; init; }
        public DbSet<Matches> Matches { get; init; }
        public DbSet<MatchTeams> MatchTeams { get; init; }
        public DbSet<Participants> Participants { get; init; }
        public DbSet<ChampionPageItems> ParticipantItems { get; init; }
        
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
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Id).ValueGeneratedOnAdd();
                entity.Property(a => a.Method).IsRequired();
                entity.Property(a => a.Exception).IsRequired();
                entity.Property(a => a.StackTrace).IsRequired();
            });

            // PatchVersions
            modelBuilder.Entity<PatchVersions>(entity =>
            {
                entity.HasKey(pv => pv.Id);
                entity.Property(pv => pv.PatchVersion).HasMaxLength(5).IsRequired();
            });
            
            // Summoners
            modelBuilder.Entity<Summoners>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.HasAlternateKey(s => s.Puuid);
                entity.HasAlternateKey(s => new { s.SummonerId, s.Platform }).IsClustered();
                entity.Property(s => s.Puuid).HasMaxLength(78).IsRequired();
                entity.Property(s => s.SummonerId).HasMaxLength(64).IsRequired();
                entity.Property(s => s.Platform).HasConversion<short>();
                entity.Property(s => s.RiotId).HasMaxLength(22).IsRequired();
                entity.Property(s => s.ProfileIconId).IsRequired();
                entity.Property(s => s.SummonerLevel).IsRequired();
                entity.Property(s => s.LastUpdated)
                    .HasDefaultValueSql("GETDATE()")
                    .IsRequired();
            });
            
            // Champions
            modelBuilder.Entity<Champions>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedNever();
                entity.Property(c => c.Name)
                    .HasMaxLength(40)
                    .IsRequired();
                entity.HasIndex(c => c.Name).IsUnique(); // Enforce unique constraint
                entity.Property(c => c.PatchLastUpdated).IsRequired();
            });
            
            // ChampionPages
            modelBuilder.Entity<ChampionPages>(entity =>
            {
                // Configure composite key
                entity.HasKey(cp => new { cp.ChampionsId, cp.Role, cp.Platform, cp.PatchVersionsId, cp.Queue, cp.Tier, cp.Division });
    
                // Enum conversions
                entity.Property(cp => cp.Role).HasConversion<byte>();
                entity.Property(cp => cp.Platform).HasConversion<byte>();
                entity.Property(cp => cp.Queue).HasConversion<short>();
                entity.Property(cp => cp.Tier).HasConversion<byte>();
                entity.Property(cp => cp.Division).HasConversion<byte>();

                // Decimal precision settings
                entity.Property(cp => cp.WinRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.PickRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.RunePageWinRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.RunePagePickRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.SummonerSpellsWinRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.SummonerSpellsPickRate).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.AttackDamagePercent).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.MagicDamagePercent).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.TrueDamagePercent).HasColumnType("decimal(9,2)");
                entity.Property(cp => cp.Pbi).HasColumnType("decimal(9,2)");
            });
            
            // ChampionPageItems
            modelBuilder.Entity<ChampionPageItems>(entity =>
            {
                entity.HasKey(cpi => new { cpi.ChampionPageId, cpi.ItemType, cpi.ItemId });
                entity.Property(cpi => cpi.ItemType).HasConversion<byte>();
                entity.Property(cpi => cpi.WinRate).HasColumnType("decimal(9,2)");
                entity.Property(cpi => cpi.PickRate).HasColumnType("decimal(9,2)");
            });

            // Queues
            modelBuilder.Entity<Queues>(entity =>
            {
                entity.HasKey(q => q.Id);
                entity.Property(q => q.Id).ValueGeneratedNever();
                entity.Property(q => q.Name).IsRequired();
            });
            
            // Matches
            modelBuilder.Entity<Matches>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).ValueGeneratedOnAdd();
                entity.Property(m => m.Platform).HasConversion<short>();
                entity.Property(m => m.GameId).IsRequired();
                entity.Property(m => m.Tier).HasConversion<byte>();
                entity.Property(m => m.Division).HasConversion<byte>();
                entity.Property(m => m.Queue).HasConversion<short>();
                entity.Property(m => m.DatePlayed).IsRequired();
                entity.Property(m => m.TimePlayed).IsRequired();
                entity.Property(m => m.WinningTeam).HasConversion<short>();
                entity.Property(m => m.PatchVersionsId).IsRequired();
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
                // Use Id as the primary key and configure it as an identity column
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.HasAlternateKey(p => new { p.SummonersId, p.MatchesId });
                entity.Property(p => p.Team).HasConversion<short>();
                entity.Property(p => p.Role).HasConversion<byte>();
                entity.Property(p => p.Kda).HasColumnType("decimal(9,2)");
                entity.Property(p => p.KillParticipation).HasColumnType("decimal(9,2)");
                entity.Property(p => p.GoldPerMinute).HasColumnType("decimal(9,2)");
                entity.Property(p => p.DamagePerMinute).HasColumnType("decimal(9,2)");


                entity.HasOne(p => p.Summoner)
                    .WithMany(s => s.MatchHistory)
                    .HasForeignKey(p => p.SummonersId);

                entity.HasOne(p => p.Match)
                    .WithMany(m => m.Participants)
                    .HasForeignKey(p => p.MatchesId);

                entity.HasOne(p => p.Champion)
                    .WithMany(c => c.Participants)
                    .HasForeignKey(p => p.ChampionsId);
            });
            
            base.OnModelCreating(modelBuilder);
        }
    }
}