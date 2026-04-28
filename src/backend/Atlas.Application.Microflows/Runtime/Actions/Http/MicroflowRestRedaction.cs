namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public static class MicroflowRestRedaction
{
    private const string Redacted = "***redacted***";

    public static IReadOnlyDictionary<string, string> RedactHeaders(
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyList<string> configuredSensitiveHeaders)
    {
        var sensitive = new HashSet<string>(configuredSensitiveHeaders, StringComparer.OrdinalIgnoreCase)
        {
            "authorization",
            "cookie",
            "set-cookie",
            "x-api-key",
            "api-key",
            "apikey",
            "proxy-authorization"
        };

        return headers.ToDictionary(
            pair => pair.Key,
            pair => ShouldRedact(pair.Key, sensitive) ? Redacted : pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public static string RedactHeaderValue(string headerName, string value, IReadOnlyList<string> configuredSensitiveHeaders)
        => ShouldRedact(headerName, new HashSet<string>(configuredSensitiveHeaders, StringComparer.OrdinalIgnoreCase))
            ? Redacted
            : value;

    private static bool ShouldRedact(string headerName, HashSet<string> sensitiveHeaders)
        => sensitiveHeaders.Contains(headerName)
            || headerName.Contains("api-key", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("secret", StringComparison.OrdinalIgnoreCase);
}
