using System.Security.Claims;

namespace Atlas.Presentation.Shared.Identity;

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

    internal static bool CanUseHeaderOverrideForAuthenticatedRequest(HttpContext context, AppOptions options)
    {
        if (!options.AllowHeaderOverrideWhenAuthenticated)
        {
            return false;
        }

        if (!options.RequireWorkspaceHeaderForAuthenticatedOverride)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.WorkspaceHeaderName)
            || !context.Request.Headers.TryGetValue(options.WorkspaceHeaderName, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.ToString().Trim();
        if (string.IsNullOrWhiteSpace(options.WorkspaceHeaderValue))
        {
            return true;
        }

        return string.Equals(value, options.WorkspaceHeaderValue, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasAuthenticationCredentials(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            return true;
        }

        return context.Request.Cookies.TryGetValue("access_token", out var accessToken)
               && !string.IsNullOrWhiteSpace(accessToken);
    }
}
