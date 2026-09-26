using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace AuthDemo.Endpoints;

/// <summary>
/// Maps three health endpoints — all anonymous, excluded from auth middleware:
///
///   GET /health        — full detail, anonymous — for ops dashboards / monitoring agents
///   GET /health/ready  — readiness probe, anonymous — Kubernetes readinessProbe
///   GET /health/live   — liveness probe, anonymous  — Kubernetes livenessProbe
///
/// Tags used to control which checks appear on which endpoint:
///   "ready"  — checks that determine if the pod should receive traffic (DB, dependencies)
///   "live"   — checks that determine if the process is still alive (self-check only)
///   (none)   — all checks appear on /health
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // ── /health — full report, anonymous ─────────────────────────────────
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedJson,
            ResultStatusCodes = DefaultStatusCodes(),
        })
        .AllowAnonymous()
        .WithTags("Health");

        // ── /health/ready — readiness probe, anonymous ────────────────────────
        // Only runs checks tagged "ready" (DB, external APIs)
        // Returns 200 when healthy/degraded, 503 when unhealthy
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteDetailedJson,
            ResultStatusCodes = new Dictionary<HealthStatus, int>
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,   // degraded still accepts traffic
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            }
        })
        .AllowAnonymous()
        .WithTags("Health");

        // ── /health/live — liveness probe, anonymous ──────────────────────────
        // Only runs checks tagged "live" — just proves the process is up
        // Returns 200 or 503
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WritePlainJson,
            ResultStatusCodes = new Dictionary<HealthStatus, int>
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            }
        })
        .AllowAnonymous()
        .WithTags("Health");

        return app;
    }

    // ── Response writers ──────────────────────────────────────────────────────

    /// <summary>Full JSON — all check names, statuses, durations, and exception details.</summary>
    private static Task WriteDetailedJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data,
                exception = e.Value.Exception?.Message
            })
        };

        return ctx.Response.WriteAsJsonAsync(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>Minimal JSON — just the overall status. Used for liveness.</summary>
    private static Task WritePlainJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow
        });
    }

    private static Dictionary<HealthStatus, int> DefaultStatusCodes() => new()
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    };
}