using Microsoft.AspNetCore.Mvc;

namespace PropelIQ.SharedKernel.Errors;

/// <summary>
/// Factory for building RFC 9457 Problem Details responses.
/// Centralises problem details construction to maintain consistent error
/// contract across all modules (TR-002 — structured API responses).
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>Creates a 500 Internal Server Error Problem Details record.</summary>
    public static ProblemDetails ServerError(string traceId) => new()
    {
        Status = 500,
        Title = "An unexpected error occurred.",
        Detail = "A server error occurred. Please try again later or contact support.",
        Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        Extensions = { ["traceId"] = traceId },
    };

    /// <summary>Creates a 400 Bad Request Problem Details record.</summary>
    public static ProblemDetails BadRequest(string detail, string traceId) => new()
    {
        Status = 400,
        Title = "Bad Request",
        Detail = detail,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        Extensions = { ["traceId"] = traceId },
    };

    /// <summary>Creates a 404 Not Found Problem Details record.</summary>
    public static ProblemDetails NotFound(string detail, string traceId) => new()
    {
        Status = 404,
        Title = "Resource not found.",
        Detail = detail,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        Extensions = { ["traceId"] = traceId },
    };
}
