using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PropelIQ.SharedKernel.Auth;

namespace PropelIQ.Api.Infrastructure.Auth;

/// <summary>
/// JWT Bearer authentication registration.
/// Signs with HMAC-SHA256; 30-second clock-skew tolerance.
/// Expired tokens receive an <c>X-Token-Expired: true</c> response header
/// so clients can trigger the refresh flow without inspecting the body.
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddPropelIQAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);

        services
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
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtSection["SigningKey"] ?? throw new InvalidOperationException(
                                "Jwt:SigningKey is missing from configuration."))),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    // Surface token expiry to clients via header so they can refresh silently.
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                            context.Response.Headers.Append("X-Token-Expired", "true");
                        return Task.CompletedTask;
                    },

                    // Return structured Problem Details 401 instead of empty 401 or redirect.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Authentication is required to access this resource.",
                            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                        };
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                        await context.Response.WriteAsJsonAsync(problem);
                    },
                };
            });

        // Authorization (policies + FallbackPolicy) are registered separately
        // in AddAppAuthorizationPolicies() called from Program.cs.

        return services;
    }
}
