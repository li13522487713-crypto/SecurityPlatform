using System.Text.Json;
using Atlas.Application.Resources;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Localization;

namespace Atlas.Presentation.Shared.Authorization;

public sealed class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly IStringLocalizer<Messages> _localizer;

    public ApiAuthorizationMiddlewareResultHandler(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.Forbidden,
                ResolveLocalizedMessage(ErrorCodes.Forbidden, "Access denied."));
            return;
        }

        if (authorizeResult.Challenged)
        {
            var code = ResolveAuthErrorCode(context);
            var fallback = code switch
            {
                ErrorCodes.TokenExpired => "Access token expired.",
                ErrorCodes.CrossTenantForbidden => "租户标识不一致",
                _ => "Authentication failed."
            };
            var statusCode = code == ErrorCodes.CrossTenantForbidden
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;
            await WriteErrorAsync(
                context,
                statusCode,
                code,
                ResolveLocalizedMessage(code, fallback));
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static string ResolveAuthErrorCode(HttpContext context)
    {
        if (context.Items.TryGetValue(AuthorizationContextKeys.AuthErrorCodeItemKey, out var value)
            && value is string code
            && !string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        return ErrorCodes.Unauthorized;
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object>.Fail(code, message, context.TraceIdentifier);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.Response.WriteAsync(json);
    }

    private string ResolveLocalizedMessage(string code, string fallbackMessage)
    {
        var resourceKey = code switch
        {
            ErrorCodes.TokenExpired => "TokenExpired",
            ErrorCodes.Forbidden => "Forbidden",
            ErrorCodes.CrossTenantForbidden => "CrossTenantForbidden",
            ErrorCodes.Unauthorized => "Unauthorized",
            _ => "AuthenticationFailed"
        };

        var localized = _localizer[resourceKey];
        return localized.ResourceNotFound ? fallbackMessage : localized.Value;
    }
}
