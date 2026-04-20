using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PropelIQ.Api.Infrastructure.HealthChecks;

/// <summary>
/// Custom JSON response writer for the health check endpoint.
/// Returns a structured payload with status, individual check results,
/// and total duration to satisfy US_002 AC-2 and NFR-002 (500 ms p95).
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2),
            }),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            timestamp = DateTimeOffset.UtcNow,
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, _options));
    }
}
