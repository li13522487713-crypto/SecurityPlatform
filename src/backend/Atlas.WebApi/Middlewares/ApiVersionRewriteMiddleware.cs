using Atlas.WebApi.Services;

namespace Atlas.WebApi.Middlewares;

public sealed class ApiVersionRewriteMiddleware
{
    private const string VersionPrefix = "/api/v1";
    private const string LegacyPrefix = "/api";
    private readonly RequestDelegate _next;
    private readonly MigrationGovernanceMetricsStore _metricsStore;

    public ApiVersionRewriteMiddleware(
        RequestDelegate next,
        MigrationGovernanceMetricsStore metricsStore)
    {
        _next = next;
        _metricsStore = metricsStore;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalPath = context.Request.Path.Value ?? string.Empty;
        var path = originalPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            await _next(context);
            return;
        }

        var rewritten = false;

        // 已有版本前缀的路径（/api/v1, /api/v2, ...）直接放行
        if (path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase) && path.Length > 6 && char.IsDigit(path[6]))
        {
            await _next(context);
            _metricsStore.Record(originalPath, context.Request.Path.Value ?? originalPath, rewritten, context.Response.StatusCode);
            return;
        }

        if (path.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = path.Substring(LegacyPrefix.Length);
            context.Request.Path = string.IsNullOrWhiteSpace(rest)
                ? VersionPrefix
                : $"{VersionPrefix}{rest}";
            rewritten = true;
        }

        await _next(context);
        _metricsStore.Record(
            originalPath,
            context.Request.Path.Value ?? originalPath,
            rewritten,
            context.Response.StatusCode);
    }
}
