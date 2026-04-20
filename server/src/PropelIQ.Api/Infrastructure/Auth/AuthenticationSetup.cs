using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace PropelIQ.Api.Infrastructure.Auth;

/// <summary>
/// Extension method for JWT Bearer authentication registration.
/// Skeleton implementation — placeholder Jwt:Key/Issuer/Audience values
/// are replaced with real identity provider config in EP-001 (auth epic).
///
/// AC-4: unauthenticated requests to [Authorize] endpoints return HTTP 401
/// with an RFC 9457 Problem Details JSON body (never a raw exception or HTML).
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddPropelIQAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    // Placeholder key — replaced with identity provider secret in EP-001.
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            configuration["Jwt:Key"] ?? "placeholder-key-replace-in-ep001")),
                };

                options.Events = new JwtBearerEvents
                {
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

        services.AddAuthorization();

        return services;
    }
}
