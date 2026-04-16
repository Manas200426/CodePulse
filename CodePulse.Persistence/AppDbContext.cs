using CodePulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MonitoredService> MonitoredServices => Set<MonitoredService>();
    public DbSet<HealthCheckResult> HealthCheckResults => Set<HealthCheckResult>();
    public DbSet<Incident> Incidents => Set<Incident>();
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
        modelBuilder.Entity<HealthCheckResult>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId);

            entity.Property(x => x.StatusCode).IsRequired();
            entity.Property(x => x.ResponseTimeMs).IsRequired();
            entity.Property(x => x.IsSuccess).IsRequired();
        });
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId);

            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.Reason).IsRequired();
        });
    }
}