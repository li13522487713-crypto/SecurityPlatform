using System.Security.Claims;

namespace Atlas.WebApi.Identity;

/// <summary>
/// Shared helpers for resolving the current application ID from HTTP context.
/// Centralises logic used by both <see cref="AppContextMiddleware"/> and
/// <see cref="HttpContextAppContextAccessor"/> to avoid duplication.
/// </summary>
internal static class AppIdResolver
{
    /// <summary>
    /// Returns the app_id claim value when the user is authenticated, otherwise null.
    /// </summary>
    internal static string? TryResolveFromClaims(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var appId = user.FindFirst("app_id")?.Value ?? user.FindFirst("appId")?.Value;
        return string.IsNullOrWhiteSpace(appId) ? null : appId;
    }

    /// <summary>
    /// Returns the header-supplied app ID when the configured header is present and valid.
    /// Returns null if the header is absent, empty, or fails the numeric guard.
    /// </summary>
    internal static string? TryResolveFromHeader(HttpContext context, AppOptions options)
    {
        var headerName = options.HeaderName;
        if (string.IsNullOrWhiteSpace(headerName)
            || !context.Request.Headers.TryGetValue(headerName, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.ToString().Trim();
        if (!options.RequireHeaderAppIdNumeric)
        {
            return value;
        }

        return long.TryParse(value, out var parsed) && parsed > 0 ? value : null;
    }
}
