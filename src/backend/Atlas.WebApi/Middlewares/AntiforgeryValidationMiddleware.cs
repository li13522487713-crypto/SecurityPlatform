using Atlas.Core.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Atlas.WebApi.Middlewares;

public sealed class AntiforgeryValidationMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    private readonly RequestDelegate _next;

    public AntiforgeryValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var payload = ApiResponse<object>.Fail(ErrorCodes.AntiforgeryTokenInvalid, "CSRF 校验失败", context.TraceIdentifier);
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        var method = context.Request.Method;
        if (SafeMethods.Contains(method))
        {
            return true;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            return true;
        }

        if (endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null)
        {
            return true;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        return false;
    }
}
