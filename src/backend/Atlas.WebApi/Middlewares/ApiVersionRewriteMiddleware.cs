namespace Atlas.WebApi.Middlewares;

public sealed class ApiVersionRewriteMiddleware
{
    private const string VersionPrefix = "/api/v1";
    private const string LegacyPrefix = "/api";
    private readonly RequestDelegate _next;

    public ApiVersionRewriteMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return _next(context);
        }

        if (path.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        if (path.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = path.Substring(LegacyPrefix.Length);
            context.Request.Path = string.IsNullOrWhiteSpace(rest)
                ? VersionPrefix
                : $"{VersionPrefix}{rest}";
        }

        return _next(context);
    }
}
