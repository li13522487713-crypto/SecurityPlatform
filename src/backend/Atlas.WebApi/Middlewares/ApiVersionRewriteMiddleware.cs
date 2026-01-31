namespace Atlas.WebApi.Middlewares;

public sealed class ApiVersionRewriteMiddleware
{
    private const string VersionPrefix = "/api/v1";
    private readonly RequestDelegate _next;

    public ApiVersionRewriteMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (!string.IsNullOrWhiteSpace(path)
            && path.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = path.Substring(VersionPrefix.Length);
            if (string.IsNullOrWhiteSpace(rest))
            {
                rest = "/api";
            }
            else if (!rest.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                rest = "/api" + rest;
            }

            context.Request.Path = rest;
        }

        return _next(context);
    }
}
