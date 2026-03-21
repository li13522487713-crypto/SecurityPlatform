using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;
using System.Text.Json;

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

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "true";
        headers["Sunset"] = _sunset;
        headers["Warning"] = $"299 - \"Deprecated API: {_message}. Use {_replacement}.\"";
        headers["X-Api-Deprecated"] = "true";
        headers["X-Api-Replacement"] = _replacement;

        await RecordDeprecatedUsageAsync(context);
        await next();
    }

    private async Task RecordDeprecatedUsageAsync(ActionExecutingContext context)
    {
        try
        {
            var services = context.HttpContext.RequestServices;
            var auditWriter = services.GetService(typeof(IAuditWriter)) as IAuditWriter;
            if (auditWriter is null)
            {
                return;
            }

            var tenantProvider = services.GetService(typeof(ITenantProvider)) as ITenantProvider;
            var userAccessor = services.GetService(typeof(ICurrentUserAccessor)) as ICurrentUserAccessor;
            var tenantId = tenantProvider?.GetTenantId() ?? default;
            var userId = userAccessor?.GetCurrentUser()?.UserId.ToString() ?? "anonymous";
            var request = context.HttpContext.Request;
            var endpoint = $"{request.Method}:{request.Path.Value ?? string.Empty}";
            var targetJson = JsonSerializer.Serialize(new
            {
                endpoint,
                replacement = _replacement,
                caller = request.Headers["User-Agent"].ToString(),
                traceId = context.HttpContext.TraceIdentifier
            });
            var record = new AuditRecord(
                tenantId,
                userId,
                "deprecated_api_called",
                "Warning",
                targetJson,
                request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                request.Headers["User-Agent"].ToString());
            await auditWriter.WriteAsync(record, context.HttpContext.RequestAborted);
        }
        catch
        {
            // 审计写入失败不影响请求处理
        }
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
