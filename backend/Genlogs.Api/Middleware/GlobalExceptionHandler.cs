using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Genlogs.Api.Middleware;

/// <summary>
/// Central catch-all: always returns ProblemDetails, never a stack trace or exception message that could
/// leak internals/secrets. Malformed-JSON-body exceptions are mapped to 400 rather than 500, since they're
/// a client input error, not a server fault.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var (statusCode, title) = exception switch
        {
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request."),
            JsonException => (StatusCodes.Status400BadRequest, "Malformed request body."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://tools.ietf.org/html/rfc9110#section-{(statusCode == StatusCodes.Status400BadRequest ? "15.5.1" : "15.6.1")}",
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
