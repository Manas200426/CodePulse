using CodePulse.Persistence;
using CodePulse.Persistence.Seed;
using Microsoft.AspNetCore.Mvc;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SetupController> _logger;

    public SetupController(AppDbContext db, ILogger<SetupController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        await DataSeeder.ResetAsync(_db, _logger);

        return Ok(new
        {
            message = "Database reset and reseeded successfully"
        });
    }
}