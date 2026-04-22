using CodePulse.Application.Services;
using CodePulse.Infrastructure.Monitoring;
using CodePulse.Persistence;
using CodePulse.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── HttpClient (used by MonitoredServiceService.RunCheckAsync) ─────────────────
builder.Services.AddHttpClient();

// ── Application services ───────────────────────────────────────────────────────
builder.Services.AddScoped<IMonitoredServiceService, MonitoredServiceService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IAnomalyService, AnomalyService>();
builder.Services.AddScoped<ICorrelationService, CorrelationService>();

// ── Background worker ──────────────────────────────────────────────────────────
builder.Services.AddHostedService<HealthCheckWorker>();

// ── CORS — allow React dev servers ────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",   // Create React App
                "http://localhost:5173"    // Vite
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Global error handler ──────────────────────────────────────────────────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        context.Response.StatusCode  = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error  = "An unexpected error occurred.",
            detail = feature?.Error?.Message
        });
    });
});

// ── Seed database on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DataSeeder.SeedAsync(db, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();           // ← must come before MapControllers
app.UseAuthorization();
app.MapControllers();
app.Run();
