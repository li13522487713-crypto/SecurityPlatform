using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atlas.WebApi.Filters;

public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    private readonly IIdempotencyRecordRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IdempotencyOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public IdempotencyFilter(
        IIdempotencyRecordRepository repository,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IOptions<IdempotencyOptions> options,
        IOptions<JsonOptions> jsonOptions)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _options = options.Value;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            await next();
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(_options.HeaderName)
            ? "Idempotency-Key"
            : _options.HeaderName;
        if (!context.HttpContext.Request.Headers.TryGetValue(headerName, out var keyValues))
        {
            context.Result = BuildErrorResult(ErrorCodes.IdempotencyRequired, "缺少幂等键", StatusCodes.Status400BadRequest, context.HttpContext.TraceIdentifier);
            return;
        }

        var idempotencyKey = keyValues.ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Result = BuildErrorResult(ErrorCodes.IdempotencyRequired, "幂等键不能为空", StatusCodes.Status400BadRequest, context.HttpContext.TraceIdentifier);
            return;
        }

        if (_options.MaxKeyLength > 0 && idempotencyKey.Length > _options.MaxKeyLength)
        {
            context.Result = BuildErrorResult(ErrorCodes.IdempotencyRequired, "幂等键长度超出限制", StatusCodes.Status400BadRequest, context.HttpContext.TraceIdentifier);
            return;
        }

        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            context.Result = BuildErrorResult(ErrorCodes.ValidationError, "缺少租户标识", StatusCodes.Status400BadRequest, context.HttpContext.TraceIdentifier);
            return;
        }

        var apiName = ResolveApiName(context.HttpContext);
        var requestHash = await ComputeRequestHashAsync(context.HttpContext);
        var now = _timeProvider.GetUtcNow();
        var existing = await _repository.FindActiveAsync(
            tenantId,
            currentUser.UserId,
            apiName,
            idempotencyKey,
            now,
            context.HttpContext.RequestAborted);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                context.Result = BuildErrorResult(ErrorCodes.IdempotencyConflict, "幂等键冲突", StatusCodes.Status409Conflict, context.HttpContext.TraceIdentifier);
                return;
            }

            if (existing.Status != IdempotencyStatus.Completed)
            {
                context.Result = BuildErrorResult(ErrorCodes.IdempotencyInProgress, "幂等键处理中，请稍后重试", StatusCodes.Status409Conflict, context.HttpContext.TraceIdentifier);
                return;
            }

            context.Result = new ContentResult
            {
                StatusCode = existing.StatusCode > 0 ? existing.StatusCode : StatusCodes.Status200OK,
                ContentType = string.IsNullOrWhiteSpace(existing.ResponseContentType) ? "application/json" : existing.ResponseContentType,
                Content = existing.ResponseBody
            };
            return;
        }

        var expiresAt = now.Add(TimeSpan.FromHours(Math.Max(1, _options.RetentionHours)));
        var record = new IdempotencyRecord(
            tenantId,
            currentUser.UserId,
            apiName,
            idempotencyKey,
            requestHash,
            now,
            expiresAt,
            _idGeneratorAccessor.NextId());

        var inserted = await _repository.TryAddAsync(record, context.HttpContext.RequestAborted);
        if (!inserted)
        {
            var concurrent = await _repository.FindActiveAsync(
                tenantId,
                currentUser.UserId,
                apiName,
                idempotencyKey,
                now,
                context.HttpContext.RequestAborted);
            if (concurrent is not null)
            {
                if (!string.Equals(concurrent.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    context.Result = BuildErrorResult(ErrorCodes.IdempotencyConflict, "幂等键冲突", StatusCodes.Status409Conflict, context.HttpContext.TraceIdentifier);
                    return;
                }

                context.Result = BuildErrorResult(ErrorCodes.IdempotencyInProgress, "幂等键处理中，请稍后重试", StatusCodes.Status409Conflict, context.HttpContext.TraceIdentifier);
                return;
            }
        }

        ActionExecutedContext? executedContext = null;
        try
        {
            executedContext = await next();
        }
        catch
        {
            await _repository.DeleteAsync(tenantId, record.Id, context.HttpContext.RequestAborted);
            throw;
        }

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            await _repository.DeleteAsync(tenantId, record.Id, context.HttpContext.RequestAborted);
            return;
        }

        if (TryGetResultPayload(executedContext.Result, out var statusCode, out var responseBody, out var contentType))
        {
            if (statusCode >= 200 && statusCode < 300)
            {
                var resourceId = ExtractResourceId(responseBody);
                record.Complete(statusCode, responseBody, contentType, resourceId, _timeProvider.GetUtcNow());
                await _repository.UpdateAsync(record, context.HttpContext.RequestAborted);
                return;
            }
        }

        await _repository.DeleteAsync(tenantId, record.Id, context.HttpContext.RequestAborted);
    }

    private static bool ShouldSkip(ActionExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (SafeMethods.Contains(method))
        {
            return true;
        }

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint is null)
        {
            return true;
        }

        if (endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null)
        {
            return true;
        }

        if (endpoint.Metadata.GetMetadata<SkipIdempotencyAttribute>() is not null)
        {
            return true;
        }

        return false;
    }

    private async Task<string> ComputeRequestHashAsync(HttpContext context)
    {
        var request = context.Request;
        request.EnableBuffering();

        string body = string.Empty;
        if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        var payload = $"{request.Path}{request.QueryString}|{body}";
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    private static string ResolveApiName(HttpContext context)
    {
        var endpoint = context.GetEndpoint() as RouteEndpoint;
        var routePattern = endpoint?.RoutePattern?.RawText;
        var method = context.Request.Method.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(routePattern))
        {
            return $"{method} {routePattern}";
        }

        var path = context.Request.Path.HasValue ? context.Request.Path.Value : "/";
        return $"{method} {path}";
    }

    private bool TryGetResultPayload(IActionResult? result, out int statusCode, out string responseBody, out string contentType)
    {
        statusCode = StatusCodes.Status200OK;
        responseBody = string.Empty;
        contentType = "application/json";

        if (result is ObjectResult objectResult)
        {
            statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            contentType = objectResult.ContentTypes.FirstOrDefault() ?? "application/json";
            responseBody = JsonSerializer.Serialize(objectResult.Value, _jsonOptions);
            return true;
        }

        if (result is StatusCodeResult statusCodeResult)
        {
            statusCode = statusCodeResult.StatusCode;
            responseBody = string.Empty;
            return true;
        }

        if (result is ContentResult contentResult)
        {
            statusCode = contentResult.StatusCode ?? StatusCodes.Status200OK;
            contentType = string.IsNullOrWhiteSpace(contentResult.ContentType) ? "text/plain" : contentResult.ContentType;
            responseBody = contentResult.Content ?? string.Empty;
            return true;
        }

        return false;
    }

    private static string? ExtractResourceId(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }

            if (data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (data.TryGetProperty("id", out var idValue) || data.TryGetProperty("Id", out idValue))
            {
                return idValue.ToString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static ObjectResult BuildErrorResult(string code, string message, int statusCode, string traceId)
    {
        var payload = ApiResponse<object>.Fail(code, message, traceId);
        return new ObjectResult(payload)
        {
            StatusCode = statusCode
        };
    }
}
