using CodePulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodePulse.Persistence.Seed;

/// <summary>
/// Seeds the database with all CodePulse.TestServices endpoints as MonitoredServices.
/// Idempotent — safe to call on every startup. Uses fixed GUIDs so it never duplicates.
/// </summary>
public static class DataSeeder
{
    // ── Fixed GUIDs so re-runs never create duplicates ─────────────────────────
    private static readonly Guid IdHealthy    = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid IdFail       = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid IdSlow       = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid IdFlaky      = new("11111111-0000-0000-0000-000000000004");
    private static readonly Guid IdDegrading  = new("11111111-0000-0000-0000-000000000005");
    private static readonly Guid IdAuth       = new("11111111-0000-0000-0000-000000000006");
    private static readonly Guid IdOrders     = new("11111111-0000-0000-0000-000000000007");

    private static readonly Guid IdDepOrdersAuth = new("22222222-0000-0000-0000-000000000001");

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        // ── 1. Pending migrations check ────────────────────────────────────────
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogWarning("[Seeder] Pending migrations found. Applying before seeding...");
            await db.Database.MigrateAsync();
        }

        // ── 2. Seed MonitoredServices ──────────────────────────────────────────
        var services = BuildServices();
        var seededCount = 0;

        foreach (var service in services)
        {
            var exists = await db.MonitoredServices.AnyAsync(x => x.Id == service.Id);
            if (!exists)
            {
                db.MonitoredServices.Add(service);
                seededCount++;
            }
        }

        // ── 3. Seed ServiceDependency: Orders → Auth ───────────────────────────
        var depExists = await db.ServiceDependencies.AnyAsync(x => x.Id == IdDepOrdersAuth);
        if (!depExists)
        {
            db.ServiceDependencies.Add(new ServiceDependency
            {
                Id                = IdDepOrdersAuth,
                ServiceId         = IdOrders,   // downstream  (orders)
                DependsOnServiceId = IdAuth      // upstream    (auth)
            });
            seededCount++;
        }

        if (seededCount > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("[Seeder] Seeded {Count} record(s) successfully.", seededCount);
        }
        else
        {
            logger.LogInformation("[Seeder] Database already seeded. Skipping.");
        }
    }

    // ── Service definitions ────────────────────────────────────────────────────
    private static List<MonitoredService> BuildServices() =>
    [
        new MonitoredService
        {
            Id                   = IdHealthy,
            Name                 = "Healthy Service",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/healthy",
            CheckIntervalSeconds = 30,
            TimeoutSeconds       = 5,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdFail,
            Name                 = "Always Failing Service",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/fail",
            CheckIntervalSeconds = 30,
            TimeoutSeconds       = 5,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdSlow,
            Name                 = "Slow Service",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/slow?delay=3000",
            CheckIntervalSeconds = 60,
            TimeoutSeconds       = 10,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdFlaky,
            Name                 = "Flaky Service",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/flaky",
            CheckIntervalSeconds = 30,
            TimeoutSeconds       = 5,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdDegrading,
            Name                 = "Degrading Service",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/degrading",
            CheckIntervalSeconds = 60,
            TimeoutSeconds       = 20,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdAuth,
            Name                 = "Auth API",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/auth",
            CheckIntervalSeconds = 30,
            TimeoutSeconds       = 5,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        },
        new MonitoredService
        {
            Id                   = IdOrders,
            Name                 = "Orders API",
            BaseUrl              = "http://localhost:5001",
            HealthEndpoint       = "/orders",
            CheckIntervalSeconds = 30,
            TimeoutSeconds       = 10,
            IsActive             = true,
            CreatedAtUtc         = DateTime.UtcNow
        }
    ];
    public static async Task ResetAsync(AppDbContext db, ILogger logger)
    {
        logger.LogWarning("[Seeder] RESETTING database...");

        // ⚠️ Order matters (FK constraints)
        await db.ServiceDependencies.ExecuteDeleteAsync();
        await db.Anomalies.ExecuteDeleteAsync();
        await db.Incidents.ExecuteDeleteAsync();
        await db.HealthCheckResults.ExecuteDeleteAsync();
        await db.MonitoredServices.ExecuteDeleteAsync();

        logger.LogInformation("[Seeder] All tables cleared.");

        // Re-seed fresh data
        await SeedAsync(db, logger);

        logger.LogInformation("[Seeder] Database reset + reseeded successfully.");
    }
}
