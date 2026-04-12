using CodePulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MonitoredService> MonitoredServices => Set<MonitoredService>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MonitoredService>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.HealthEndpoint)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.CheckIntervalSeconds)
                .IsRequired();

            entity.Property(x => x.TimeoutSeconds)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();
        });
    }
}