using Atlas.Application.Resources;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Text.Json;

namespace Atlas.WebApi.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            var message = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ErrorCodes.ValidationError, message);
        }
        catch (BusinessException ex)
        {
            var statusCode = MapStatusCode(ex.Code);
            await WriteErrorAsync(context, statusCode, ex.Code, ResolveLocalizedMessage(context, ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(
                context,
                HttpStatusCode.InternalServerError,
                ErrorCodes.ServerError,
                ResolveLocalizedMessage(context, ErrorCodes.ServerError, "服务器内部错误"));
        }
    }

    private static HttpStatusCode MapStatusCode(string code)
    {
        return code switch
        {
            ErrorCodes.Conflict => HttpStatusCode.Conflict,
            ErrorCodes.Unauthorized => HttpStatusCode.Unauthorized,
            ErrorCodes.Forbidden => HttpStatusCode.Forbidden,
            ErrorCodes.AccountLocked => HttpStatusCode.Forbidden,
            ErrorCodes.PasswordExpired => HttpStatusCode.Forbidden,
            ErrorCodes.MfaRequired => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.BadRequest
        };
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var traceId = context.TraceIdentifier;
        var payload = ApiResponse<ProblemDetails>.Fail(code, message, traceId);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.Response.WriteAsync(json);
    }

    private static string ResolveLocalizedMessage(HttpContext context, string code, string fallbackMessage)
    {
        var localizer = context.RequestServices.GetService<IStringLocalizer<Messages>>();
        if (localizer is null)
        {
            return fallbackMessage;
        }

        var directLocalized = localizer[code];
        if (!directLocalized.ResourceNotFound)
        {
            return directLocalized.Value;
        }

        var resourceKey = code switch
        {
            ErrorCodes.ValidationError => "ValidationError",
            ErrorCodes.Unauthorized => "Unauthorized",
            ErrorCodes.Forbidden => "Forbidden",
            ErrorCodes.NotFound => "NotFound",
            ErrorCodes.ServerError => "InternalError",
            ErrorCodes.AccountLocked => "AccountLocked",
            ErrorCodes.PasswordExpired => "PasswordExpired",
            _ => null
        };

        if (resourceKey is null)
        {
            return fallbackMessage;
        }

        var localized = localizer[resourceKey];
        return localized.ResourceNotFound ? fallbackMessage : localized.Value;
    }
}
