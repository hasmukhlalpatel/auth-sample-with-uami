using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthDemo.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }

    // GET /api/weather/admin — requires the Admin role
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAdminReport()
    {
        var callerName = User.Identity?.Name ?? "unknown";

        return Ok(new
        {
            Message = "Sensitive admin-only weather report",
            RequestedBy = callerName,
            ServerTime = DateTime.UtcNow,
            SecretStation = "Station Delta-9",
        });
    }

    // GET /api/weather/me — any authenticated user, shows their claims
    [HttpGet("me")]
    public IActionResult WhoAmI()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(new
        {
            User.Identity?.Name,
            Roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value),
            Claims = claims
        });
    }
}
