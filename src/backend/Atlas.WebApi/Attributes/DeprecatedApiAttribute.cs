using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

namespace Atlas.WebApi.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DeprecatedApiAttribute : ActionFilterAttribute
{
    private const string DefaultSunsetHttpDate = "Thu, 17 Sep 2026 00:00:00 GMT";
    private readonly string _message;
    private readonly string _sunset;
    private readonly string _replacement;

    public DeprecatedApiAttribute(string message, string replacement, string? sunset = null)
    {
        _message = message;
        _replacement = replacement;
        _sunset = ToHttpDateOrDefault(sunset);
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "true";
        headers["Sunset"] = _sunset;
        headers["Warning"] = $"299 - \"Deprecated API: {_message}. Use {_replacement}.\"";
        headers["X-Api-Deprecated"] = "true";
        headers["X-Api-Replacement"] = _replacement;
        base.OnActionExecuting(context);
    }

    private static string ToHttpDateOrDefault(string? sunset)
    {
        if (string.IsNullOrWhiteSpace(sunset))
        {
            return DefaultSunsetHttpDate;
        }

        return DateTimeOffset.TryParse(sunset, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime.ToString("R", CultureInfo.InvariantCulture)
            : sunset;
    }
}
