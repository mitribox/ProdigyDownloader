using ClonerApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Data;

public sealed class ClonerDbContext : DbContext
{
    public ClonerDbContext(DbContextOptions<ClonerDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CrawlRun> Runs => Set<CrawlRun>();
    public DbSet<Asset> Assets => Set<Asset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.StartUrls).IsRequired();
            e.Property(x => x.OutputRoot).IsRequired();
            e.Property(x => x.SelectedExtensions).HasMaxLength(500);
            e.Property(x => x.ScheduleTime).HasMaxLength(16);
            e.Property(x => x.ScheduleDays).HasMaxLength(64);
            e.Property(x => x.UrlPrefix).HasMaxLength(2048);
            e.Property(x => x.UrlRegex).HasMaxLength(1024);
            e.HasIndex(x => x.NextRunAtUtc);
        });

        modelBuilder.Entity<CrawlRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Project)
                .WithMany(x => x.Runs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProjectId);
        });

        modelBuilder.Entity<Asset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NormalizedUrl).HasMaxLength(2048).IsRequired();
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.ETag).HasMaxLength(256);
            e.Property(x => x.Extension).HasMaxLength(16);
            e.HasOne(x => x.Project)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProjectId, x.NormalizedUrl }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.ContentHash });
        });
    }
}
