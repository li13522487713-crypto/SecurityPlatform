using Microsoft.Extensions.Logging;

namespace Atlas.WebApi.Middlewares;

public sealed class HttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;

    public HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTimeOffset.UtcNow;
        await _next(context);
        var elapsed = DateTimeOffset.UtcNow - start;

        _logger.LogInformation(
            "HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs} ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString.Value,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
}