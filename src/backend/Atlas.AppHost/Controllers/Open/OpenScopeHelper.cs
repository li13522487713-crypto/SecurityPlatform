using System.Security.Claims;

namespace Atlas.AppHost.Controllers.Open;

internal static class OpenScopeHelper
{
    public static bool HasScope(ClaimsPrincipal user, string requiredScope)
    {
        var scopes = user.FindAll("scope")
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (scopes.Length == 0)
        {
            return false;
        }

        return scopes.Any(scope =>
            string.Equals(scope, "*", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "open:*", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }
}
