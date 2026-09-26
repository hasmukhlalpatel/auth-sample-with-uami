using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthDemo.HealthChecks;

/// <summary>
/// Simulates a database connectivity check.
/// Replace the body with a real DbContext.Database.CanConnectAsync() call.
/// </summary>
public class DbHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: inject your DbContext and call:
            //   await _db.Database.CanConnectAsync(cancellationToken);
            await Task.Delay(10, cancellationToken);   // simulate I/O

            return HealthCheckResult.Healthy("Database connection OK", data: new Dictionary<string, object>
            {
                ["server"] = "db.internal",
                ["responseTimeMs"] = 10
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", ex);
        }
    }
}

/// <summary>
/// Simulates an outbound HTTP dependency check (e.g. a payment gateway, identity provider).
/// Replace the URL with your real dependency.
/// </summary>
public class ExternalApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("HealthCheckClient");
            var response = await client.GetAsync("https://httpbin.org/status/200", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("External API reachable", data: new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["url"] = "https://httpbin.org/status/200"
                })
                : HealthCheckResult.Degraded($"External API returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External API unreachable", ex);
        }
    }
}