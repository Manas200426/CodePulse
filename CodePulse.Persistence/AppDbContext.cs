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
    public DbSet<Anomaly> Anomalies => Set<Anomaly>();
    public DbSet<ServiceDependency> ServiceDependencies => Set<ServiceDependency>();
    public DbSet<IncidentCorrelation> IncidentCorrelations => Set<IncidentCorrelation>();
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

            entity.Property(x => x.Status)
                .HasConversion<string>()   // store as string in DB
                .IsRequired();
            entity.Property(x => x.Severity)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(x => x.Reason).IsRequired();
        });

        modelBuilder.Entity<Anomaly>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId);

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.CurrentValue).IsRequired();
            entity.Property(x => x.BaselineValue).IsRequired();
            entity.Property(x => x.Deviation).IsRequired();
        });
        modelBuilder.Entity<ServiceDependency>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<IncidentCorrelation>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Two FK references to the same Incidents table — must name them explicitly
            entity.HasOne(x => x.DownstreamIncident)
                .WithMany()
                .HasForeignKey(x => x.DownstreamIncidentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpstreamIncident)
                .WithMany()
                .HasForeignKey(x => x.UpstreamIncidentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.ConfidenceScore).IsRequired();
            entity.Property(x => x.TimeDifferenceMinutes).IsRequired();

            // Prevent the worker from creating duplicate rows for the same pair
            entity.HasIndex(x => new { x.DownstreamIncidentId, x.UpstreamIncidentId })
                  .IsUnique();
        });
    }
}