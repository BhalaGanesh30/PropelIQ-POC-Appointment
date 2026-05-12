using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// ASP.NET Core middleware that injects <c>"aiFallbackActive": true</c> into all
/// <c>/api/v1/</c> JSON responses while the AI gateway circuit breaker is open (US_053, Edge Case 2, AC-2).
///
/// Behaviour:
/// <list type="bullet">
///   <item><b>Fast path</b>: when circuit is <c>closed</c>, requests pass through without any
///     response buffering — zero performance overhead for the healthy steady state.</item>
///   <item><b>Fallback path</b>: when circuit is <c>open</c> or <c>half-open</c>, the response body
///     is buffered in a <see cref="MemoryStream"/>. After the downstream pipeline completes,
///     the JSON body is parsed via <see cref="JsonDocument"/> and the field is injected before
///     being written to the real response stream. Only JSON object responses are modified;
///     non-object responses (arrays, primitives) are forwarded unchanged.</item>
/// </list>
///
/// Security:
/// <list type="bullet">
///   <item>Only operates on the <c>/api/v1/</c> path prefix — health and metrics endpoints are unaffected.</item>
///   <item>JSON parsing is done via <see cref="JsonDocument"/> (safe streaming parser — no arbitrary code exec).</item>
///   <item>Parse errors silently fall back to returning the original body to prevent data loss.</item>
/// </list>
///
/// Registration: <c>app.UseMiddleware&lt;AiFallbackEnvelopeMiddleware&gt;()</c> placed after
/// <see cref="CorrelationIdMiddleware"/> and before <c>app.UseAuthentication()</c> in Program.cs.
/// </summary>
public sealed class AiFallbackEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAiGatewayStateService _stateService;
    private readonly ILogger<AiFallbackEnvelopeMiddleware> _logger;

    private static readonly PathString ApiV1Prefix = new("/api/v1");

    public AiFallbackEnvelopeMiddleware(
        RequestDelegate next,
        IAiGatewayStateService stateService,
        ILogger<AiFallbackEnvelopeMiddleware> logger)
    {
        _next         = next;
        _stateService = stateService;
        _logger       = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Fast path: circuit closed or path outside /api/v1/ → no buffering.
        if (!context.Request.Path.StartsWithSegments(ApiV1Prefix) ||
            !_stateService.IsCircuitOpen())
        {
            await _next(context);
            return;
        }

        // Circuit open: buffer the response so we can inject the fallback flag.
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Seek(0, SeekOrigin.Begin);

            var contentType = context.Response.ContentType ?? string.Empty;
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var original = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true)
                    .ReadToEndAsync();

                var modified = TryInjectFallbackActive(original);
                var bytes    = Encoding.UTF8.GetBytes(modified);

                // Update Content-Length to match modified body (headers not yet sent to network
                // because we replaced the body stream before calling _next).
                if (!context.Response.HasStarted)
                    context.Response.ContentLength = bytes.Length;

                context.Response.Body = originalBody;
                await originalBody.WriteAsync(bytes);
            }
            else
            {
                // Non-JSON response (e.g. stream download): forward buffer unchanged.
                buffer.Seek(0, SeekOrigin.Begin);
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
            }
        }
        finally
        {
            // Always restore original body to prevent response stream leaks.
            context.Response.Body = originalBody;
        }
    }

    // ── Private ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="json"/> and injects <c>"aiFallbackActive": true</c> at the end
    /// of the root object. If the root is not an object (e.g. array), or if parsing fails,
    /// returns the original string unchanged to prevent data loss.
    /// </summary>
    private string TryInjectFallbackActive(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return json; // Arrays and primitives cannot have a field injected safely.

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Skip any existing aiFallbackActive to avoid duplicates (idempotent injection).
                if (!prop.NameEquals("aiFallbackActive"))
                    prop.WriteTo(writer);
            }
            writer.WriteBoolean("aiFallbackActive", true);
            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException ex)
        {
            // Log at debug level (not warning) — malformed bodies from downstream are not
            // this middleware's concern; fallback to forwarding unchanged.
            _logger.LogDebug(ex,
                "AiFallbackEnvelopeMiddleware: failed to parse JSON response for aiFallbackActive injection. " +
                "Forwarding original body.");
            return json;
        }
    }
}
