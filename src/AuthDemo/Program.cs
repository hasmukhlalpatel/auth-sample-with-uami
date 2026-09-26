
using AuthDemo.Endpoints;
using AuthDemo.Extensions;
using AuthDemo.HealthChecks;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

namespace AuthDemo;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddConsole();
        // Add services to the container.
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.AddAuthenticationAndAuthorization();

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is running"), tags: ["live", "ready"])
            .AddCheck<DbHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
            .AddCheck<ExternalApiHealthCheck>("external-api", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            //app.MapOpenApi("/openapi/{documentName}.yaml"); // GET /openapi/v1.yaml
            app.MapOpenApi("/openapi/{documentName}.json"); // GET /openapi/v1.json
                                                            // app.MapScalarApiReference();              // GET /scalar  — interactive UI
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("My API")
                    .AddPreferredSecuritySchemes("Bearer")
                    .AddHttpAuthentication("Bearer", auth =>
                    {
                        // Leave empty so the user pastes their own token in Scalar UI
                        // Or pre-fill for dev: auth.Token = "dev-token-here";
                        auth.Token = "dev-token-here";
                    });
            });
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapPost("/api/auth/login", (LoginRequest req, IUserService users, ITokenService tokens, IConfiguration config) =>
        {
            var (valid, roles) = users.Validate(req.Username, req.Password);
            if (!valid)
                return Results.Unauthorized();

            var token = tokens.GenerateToken(req.Username, roles);
            var expiry = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:ExpiryMinutes"]!));

            return Results.Ok(new LoginResponse(token, req.Username, roles, expiry));
        })
        .WithTags("Auth")
        .WithName("Login")
        .WithSummary("Exchange credentials for a JWT")
        .AllowAnonymous();

        app.MapHealthEndpoints();
        app.MapProductEndpoints();
        app.MapControllers();

        app.Run();
    }
}
