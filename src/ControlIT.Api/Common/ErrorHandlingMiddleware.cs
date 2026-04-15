// ErrorHandlingMiddleware.cs — Global exception handler for the API pipeline.
// Pattern: Middleware (ASP.NET Core pipeline interceptor)
//
// In ASP.NET Core, middleware is a chain of functions that process HTTP requests.
// Think of it like Express.js middleware (app.use(...)). Each middleware calls _next(context)
// to pass the request to the next handler in the chain.
//
// This middleware wraps the entire pipeline in a try/catch so that any unhandled exception
// from ANY endpoint is caught here and converted to a structured JSON error response.
// It is registered FIRST in the pipeline so it catches errors from all subsequent middleware.

namespace ControlIT.Api.Common;

/// <summary>
/// Catches all unhandled exceptions and converts them to structured JSON error responses.
/// Registered as the first middleware in Program.cs so it wraps the entire pipeline.
/// </summary>
public class ErrorHandlingMiddleware
{
    // _next is a delegate to the next middleware in the pipeline.
    // In TypeScript terms: it's like a "next()" function in Express middleware.
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    // Constructor injection — ASP.NET Core's DI container provides these automatically.
    // In TypeScript, this would be like having the framework inject dependencies via decorators.
    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// InvokeAsync is called by the ASP.NET Core pipeline on every HTTP request.
    /// In Express.js terms: this IS the middleware function (req, res, next) => {...}
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass control to the next middleware (auth, rate limiter, endpoint handler, etc.)
            await _next(context);
        }
        catch (TimeoutException ex)
        {
            // Timeout means the device didn't respond within the allowed window.
            // 504 Gateway Timeout is the semantically correct HTTP status for this.
            _logger.LogWarning(ex, "Command dispatch timed out.");
            context.Response.StatusCode = 504;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Command timed out. The endpoint did not respond within the allowed window.",
                detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            // InvalidOperationException is used throughout ControlIT for "service unavailable" scenarios.
            // Examples: SignalR not connected, TenantContext not resolved, device already has pending command.
            // 503 Service Unavailable — the server can't process the request right now.
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Catch-all for anything we didn't expect.
            // Log as Error (not Warning) because this is unexpected.
            // Return a generic message to the client — don't leak internal exception details.
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
        }
    }
}
