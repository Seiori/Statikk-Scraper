using Microsoft.EntityFrameworkCore;
using Sta.Data.Models;
using Statikk_Scraper.Data.Models;
using Statikk_Scraper.Models;
using Statikk_Scraper.Statikk_Scraper.Data.Models;

namespace Statikk_Scraper.Data;

public class Context(DbContextOptions<Context> options) : DbContext(options)
{
    public DbSet<Champions> Champions { get; set; }
    public DbSet<Matches> Matches { get; set; }
    public DbSet<MatchTeamBans> MatchTeamBans { get; set; }
    public DbSet<MatchTeams> MatchTeams { get; set; }
    public DbSet<ParticipantItems> ParticipantItems { get; set; }
    public DbSet<Participants> Participants { get; set; }
    public DbSet<ParticipantSummonerSpells> ParticipantSummonerSpells { get; set; }
    public DbSet<Patches> Patches { get; set; }
    public DbSet<SummonerRanks> SummonerRanks { get; set; }
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

        // MatchTeamBans
        modelBuilder.Entity<MatchTeamBans>(entity =>
        {
            entity.HasKey(mtb => new { mtb.MatchTeamsId, mtb.ChampionsId });
            
            entity
                .HasOne(mtb => mtb.MatchTeam)
                .WithMany(mt => mt.Bans)
                .HasForeignKey(mtb => mtb.MatchTeamsId);
            
            entity
                .HasOne(mtb => mtb.Champion)
                .WithMany(c => c.Bans)
                .HasForeignKey(mtb => mtb.ChampionsId);
        });
        
        // MatchTeams
        modelBuilder.Entity<MatchTeams>(entity =>
        {
            entity.HasKey(mt => mt.Id);
            entity.HasAlternateKey(mt => new { mt.MatchesId, mt.TeamId });
            
            entity.Property(mt => mt.Id).ValueGeneratedOnAdd();
            
            entity
                .HasOne(mt => mt.Match)
                .WithMany(m => m.Teams)
                .HasForeignKey(mt => mt.MatchesId);
        });
        
        // ParticipantItems
        modelBuilder.Entity<ParticipantItems>(entity =>
        {
            entity.HasKey(pi => new { pi.ParticipantId, pi.ItemId });
            
            entity
                .HasOne(pi => pi.Participant)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.ParticipantId);
        });
        
        // Participants
        modelBuilder.Entity<Participants>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasAlternateKey(p => new { p.SummonersId, p.MatchTeamsId });
            
            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.Role).HasConversion<byte>();

            entity
                .HasOne(p => p.Summoner)
                .WithMany(s => s.Participants)
                .HasForeignKey(p => p.SummonersId);
            
            entity
                .HasOne(p => p.Team)
                .WithMany(mt => mt.Participants)
                .HasForeignKey(p => p.MatchTeamsId);

            entity
                .HasOne(p => p.Champion)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.ChampionsId);
        });
        
        // ParticipantSummonerSpells
        modelBuilder.Entity<ParticipantSummonerSpells>(entity =>
        {
            entity.HasKey(pss => new { pss.ParticipantId, pss.SummonerSpellId });
            
            entity
                .HasOne(pss => pss.Participant)
                .WithMany(s => s.SummonerSpells)
                .HasForeignKey(pss => pss.ParticipantId);
        });
        
        // Patches
        modelBuilder.Entity<Patches>(entity =>
        {
            entity.HasKey(pv => pv.Id);
            entity.HasAlternateKey(pv => pv.PatchVersion);
            
            entity.Property(pv => pv.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.PatchVersion).HasMaxLength(5);
        });
        
        // SummonerRanks
        modelBuilder.Entity<SummonerRanks>(entity =>
        {
            entity.HasKey(sr => new { sr.SummonersId, sr.Queue });
            
            entity.Ignore(sr => sr.Puuid);
            entity.Property(sr => sr.Division).HasConversion<byte>();
            
            entity
                .HasOne(sr => sr.Summoner)
                .WithMany(s => s.Ranks)
                .HasForeignKey(sr => sr.SummonersId);
        });
        
        // Summoners
        modelBuilder.Entity<Summoners>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasAlternateKey(s => s.Puuid);
            entity.HasAlternateKey(s => new { s.Region, s.SummonerId });
            
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.Puuid).HasMaxLength(78);
            entity.Property(s => s.SummonerId).HasMaxLength(58);
            entity.Property(s => s.Region).HasConversion<byte>();
            entity.Property(s => s.RiotId).HasMaxLength(40);
            entity.Property(s => s.LastUpdated).ValueGeneratedOnAdd();
        });
        
        base.OnModelCreating(modelBuilder);
    }
}