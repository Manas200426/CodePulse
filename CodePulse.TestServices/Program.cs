// ============================================================
//  CodePulse.TestServices
//  A simulation API used to test CodePulse monitoring:
//    - incident detection  (fail, flaky)
//    - anomaly detection   (slow, degrading)
//    - correlation         (auth -> orders dependency)
//  Runs on: http://localhost:5001
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// Reuse a single HttpClient for the /orders -> /auth internal call
builder.Services.AddHttpClient("internal", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

var random = new Random();

// Shared degrading counter (resets on app restart)
int degradingRequestCount = 0;

// ─────────────────────────────────────────────────────────────
// HELPER: log every request with timing + status
// ─────────────────────────────────────────────────────────────
static void Log(string endpoint, long responseTimeMs, int statusCode)
{
    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {endpoint,-20} | {responseTimeMs,6}ms | HTTP {statusCode}");
}

// ─────────────────────────────────────────────────────────────
// 1. GET /healthy — always 200
// ─────────────────────────────────────────────────────────────
app.MapGet("/healthy", async () =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await Task.CompletedTask;
    sw.Stop();

    Log("/healthy", sw.ElapsedMilliseconds, 200);
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

// ─────────────────────────────────────────────────────────────
// 2. GET /fail — always 500
// ─────────────────────────────────────────────────────────────
app.MapGet("/fail", async () =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await Task.CompletedTask;
    sw.Stop();

    Log("/fail", sw.ElapsedMilliseconds, 500);
    return Results.Json(
        new { error = "Simulated failure", timestamp = DateTime.UtcNow },
        statusCode: 500
    );
});

// ─────────────────────────────────────────────────────────────
// 3. GET /slow?delay=ms — waits given ms then 200
// ─────────────────────────────────────────────────────────────
app.MapGet("/slow", async (int delay = 2000) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    var clampedDelay = Math.Clamp(delay, 0, 30_000);
    await Task.Delay(clampedDelay);

    sw.Stop();
    Log("/slow", sw.ElapsedMilliseconds, 200);

    return Results.Ok(new
    {
        status = "ok",
        requestedDelayMs = clampedDelay,
        actualMs = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow
    });
});

// ─────────────────────────────────────────────────────────────
// 4. GET /flaky — 70% success, 30% failure
// ─────────────────────────────────────────────────────────────
app.MapGet("/flaky", async () =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await Task.CompletedTask;
    sw.Stop();

    var success = random.NextDouble() > 0.30;
    var statusCode = success ? 200 : 500;
    Log("/flaky", sw.ElapsedMilliseconds, statusCode);

    return success
        ? Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow })
        : Results.Json(new { error = "Flaky failure", timestamp = DateTime.UtcNow }, statusCode: 500);
});

// ─────────────────────────────────────────────────────────────
// 5. GET /degrading — delay doubles every request
//    1st=100ms  2nd=200ms  3rd=400ms  4th=800ms ...
//    Triggers anomaly detection in CodePulse
// ─────────────────────────────────────────────────────────────
app.MapGet("/degrading", async () =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    var count = Interlocked.Increment(ref degradingRequestCount);
    var delayMs = (int)(100 * Math.Pow(2, count - 1));
    delayMs = Math.Min(delayMs, 15_000); // cap at 15s

    await Task.Delay(delayMs);
    sw.Stop();

    Log("/degrading", sw.ElapsedMilliseconds, 200);
    return Results.Ok(new
    {
        status = "ok",
        requestNumber = count,
        simulatedDelayMs = delayMs,
        timestamp = DateTime.UtcNow
    });
});

// ─────────────────────────────────────────────────────────────
// 5a. POST /degrading/reset — resets the counter to 0
// ─────────────────────────────────────────────────────────────
app.MapPost("/degrading/reset", () =>
{
    Interlocked.Exchange(ref degradingRequestCount, 0);
    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] /degrading/reset     | counter reset to 0");
    return Results.Ok(new { message = "Counter reset to 0", timestamp = DateTime.UtcNow });
});

// ─────────────────────────────────────────────────────────────
// 6. GET /auth — upstream dependency
//    80% -> 200   20% -> 500
// ─────────────────────────────────────────────────────────────
app.MapGet("/auth", async () =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await Task.CompletedTask;
    sw.Stop();

    var success = random.NextDouble() > 0.20;
    var statusCode = success ? 200 : 500;
    Log("/auth", sw.ElapsedMilliseconds, statusCode);

    return success
        ? Results.Ok(new { status = "authenticated", timestamp = DateTime.UtcNow })
        : Results.Json(new { error = "Auth service failure", timestamp = DateTime.UtcNow }, statusCode: 500);
});

// ─────────────────────────────────────────────────────────────
// 7. GET /orders — depends on /auth
//    Calls /auth internally. If auth fails -> orders fails.
//    KEY endpoint for testing incident correlation in CodePulse.
// ─────────────────────────────────────────────────────────────
app.MapGet("/orders", async (IHttpClientFactory httpClientFactory) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        var client = httpClientFactory.CreateClient("internal");
        var authResponse = await client.GetAsync("/auth");
        sw.Stop();

        if (!authResponse.IsSuccessStatusCode)
        {
            Log("/orders", sw.ElapsedMilliseconds, 500);
            return Results.Json(new
            {
                error = "Orders failed: upstream Auth returned failure",
                authStatus = (int)authResponse.StatusCode,
                timestamp = DateTime.UtcNow
            }, statusCode: 500);
        }

        Log("/orders", sw.ElapsedMilliseconds, 200);
        return Results.Ok(new
        {
            status = "ok",
            message = "Orders processed successfully",
            authVerified = true,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        sw.Stop();
        Log("/orders", sw.ElapsedMilliseconds, 500);
        return Results.Json(new
        {
            error = $"Orders failed: {ex.Message}",
            timestamp = DateTime.UtcNow
        }, statusCode: 500);
    }
});

// ─────────────────────────────────────────────────────────────
// Startup banner
// ─────────────────────────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════╗");
    Console.WriteLine("║      CodePulse.TestServices  :5001            ║");
    Console.WriteLine("╠══════════════════════════════════════════════╣");
    Console.WriteLine("║  GET  /healthy           always 200           ║");
    Console.WriteLine("║  GET  /fail              always 500           ║");
    Console.WriteLine("║  GET  /slow?delay=2000   waits then 200       ║");
    Console.WriteLine("║  GET  /flaky             70% ok / 30% fail    ║");
    Console.WriteLine("║  GET  /degrading         doubles delay each   ║");
    Console.WriteLine("║  POST /degrading/reset   reset counter        ║");
    Console.WriteLine("║  GET  /auth              80% ok / 20% fail    ║");
    Console.WriteLine("║  GET  /orders            depends on /auth     ║");
    Console.WriteLine("╚══════════════════════════════════════════════╝");
    Console.WriteLine();
});

app.Run("http://localhost:5001");
