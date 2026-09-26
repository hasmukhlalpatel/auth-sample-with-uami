using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AuthDemo.Extensions;

public static class HostingExtensions
{
    public static void AddAuthenticationAndAuthorization(this IHostApplicationBuilder builder)
    {
        builder.AddAuthentication();
        builder.Services.AddAuthorization();
    }

    public static void AddAuthentication(this IHostApplicationBuilder builder)
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"]!;


        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero   // no grace window on token expiry
                };

                options.Events = new JwtBearerEvents
                {
                    // ── Fires when the token fails validation ─────────────────────────
                    // This is where you get the exact reason: expired, bad signature,
                    // wrong issuer/audience, malformed, etc.
                    OnAuthenticationFailed = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();

                        var reason = ctx.Exception switch
                        {
                            SecurityTokenExpiredException e =>
                                $"Token expired at {e.Expires:u}",
                            SecurityTokenNotYetValidException e =>
                                $"Token not valid before {e.NotBefore:u}",
                            SecurityTokenInvalidSignatureException =>
                                "Token signature is invalid (wrong secret key?)",
                            SecurityTokenInvalidIssuerException e =>
                                $"Invalid issuer: '{e.InvalidIssuer}'",
                            SecurityTokenInvalidAudienceException e =>
                                $"Invalid audience: '{string.Join(", ", e.InvalidAudience)}'",
                            SecurityTokenMalformedException =>
                                "Token is malformed (not a valid JWT structure)",
                            SecurityTokenNoExpirationException =>
                                "Token has no expiration claim",
                            SecurityTokenInvalidLifetimeException =>
                                "Token lifetime is invalid",
                            _ =>
                                ctx.Exception.Message
                        };

                        logger.LogWarning(
                            ctx.Exception,
                            "JWT authentication failed on {Method} {Path} — {Reason}",
                            ctx.Request.Method,
                            ctx.Request.Path,
                            reason);

                        // Surface the reason in the response header (useful in dev/staging;
                        // remove or gate behind IsDevelopment() in production)
                        ctx.Response.Headers["WWW-Authenticate-Error"] = reason;

                        return Task.CompletedTask;
                    },

                    // ── Fires before validation — lets you log the raw token ──────────
                    // Useful in dev to confirm the token is actually being received.
                    // Gate this behind IsDevelopment() — never log tokens in production.
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.HttpContext.RequestServices
                                .GetRequiredService<IHostEnvironment>()
                                .IsDevelopment())
                        {
                            var logger = ctx.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();

                            var token = ctx.Request.Headers.Authorization.ToString();

                            if (string.IsNullOrEmpty(token))
                                logger.LogDebug("JWT: no Authorization header on {Method} {Path}",
                                    ctx.Request.Method, ctx.Request.Path);
                            else
                                logger.LogDebug("JWT: token received ({Length} chars) on {Method} {Path}",
                                    token.Length, ctx.Request.Method, ctx.Request.Path);
                        }

                        return Task.CompletedTask;
                    },

                    // ── Returns a JSON 401 body instead of an empty response ──────────
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";

                        // If OnAuthenticationFailed already set a reason header, echo it
                        // in the body too so API clients get a useful message.
                        var reason = ctx.Response.Headers["WWW-Authenticate-Error"].ToString();

                        await ctx.Response.WriteAsJsonAsync(new
                        {
                            error = "Unauthorized. Provide a valid Bearer token.",
                            detail = string.IsNullOrEmpty(reason) ? null : reason
                        });
                    },

                    OnForbidden = async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsJsonAsync(new
                        {
                            error = "Forbidden. You do not have the required role."
                        });
                    }
                };
            });
    }
}
