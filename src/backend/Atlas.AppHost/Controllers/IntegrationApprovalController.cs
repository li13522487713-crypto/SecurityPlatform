using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 跨系统审批集成 API
/// 外部系统使用 API Key 认证（X-Api-Key header）发起/查询/取消审批
/// </summary>
[ApiController]
[Route("api/v1/integration/approvals")]
public sealed class IntegrationApprovalController : ControllerBase
{
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IWebhookService _webhookService;
    private readonly IApiKeyValidationService _apiKeyValidation;

    public IntegrationApprovalController(
        IApprovalRuntimeCommandService commandService,
        IApprovalRuntimeQueryService queryService,
        IWebhookService webhookService,
        IApiKeyValidationService apiKeyValidation)
    {
        _commandService = commandService;
        _queryService = queryService;
        _webhookService = webhookService;
        _apiKeyValidation = apiKeyValidation;
    }

    /// <summary>外部系统发起审批</summary>
    [HttpPost("start")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Start(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-Tenant-Id")] string? tenantIdHeader,
        [FromBody] IntegrationStartApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyRequired"), HttpContext.TraceIdentifier));
        }

        if (!Guid.TryParse(tenantIdHeader, out var tenantGuid))
        {
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "IntegrationTenantIdInvalid"), HttpContext.TraceIdentifier));
        }

        var tenantId = new TenantId(tenantGuid);

        if (!await _apiKeyValidation.ValidateAsync(tenantId, apiKey, "approval:write", cancellationToken))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyInvalid"), HttpContext.TraceIdentifier));
        }

        // 将 FormData 与 ExtraData 合并为一个 DataJson 对象，避免字段丢失
        string? mergedDataJson = null;
        if (!string.IsNullOrWhiteSpace(request.FormData) || !string.IsNullOrWhiteSpace(request.ExtraData))
        {
            var merged = new Dictionary<string, JsonElement>();
            if (!string.IsNullOrWhiteSpace(request.FormData))
            {
                try
                {
                    var formDoc = JsonDocument.Parse(request.FormData);
                    foreach (var prop in formDoc.RootElement.EnumerateObject())
                        merged[prop.Name] = prop.Value.Clone();
                }
                catch (JsonException)
                {
                    merged["formData"] = JsonDocument.Parse(JsonSerializer.Serialize(request.FormData)).RootElement.Clone();
                }
            }
            if (!string.IsNullOrWhiteSpace(request.ExtraData))
            {
                try
                {
                    var extraDoc = JsonDocument.Parse(request.ExtraData);
                    foreach (var prop in extraDoc.RootElement.EnumerateObject())
                        merged[prop.Name] = prop.Value.Clone();
                }
                catch (JsonException)
                {
                    merged["extraData"] = JsonDocument.Parse(JsonSerializer.Serialize(request.ExtraData)).RootElement.Clone();
                }
            }
            mergedDataJson = JsonSerializer.Serialize(merged);
        }

        var startRequest = new ApprovalStartRequest
        {
            DefinitionId = request.FlowDefinitionId,
            BusinessKey = request.BusinessKey,
            Title = request.Title,
            DataJson = mergedDataJson
        };

        var instance = await _commandService.StartAsync(tenantId, startRequest, request.InitiatorUserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            InstanceId = instance.Id,
            Status = instance.Status,
            BusinessKey = instance.BusinessKey
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>查询审批实例状态</summary>
    [HttpGet("{instanceId:long}/status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetStatus(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-Tenant-Id")] string? tenantIdHeader,
        long instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyRequired"), HttpContext.TraceIdentifier));
        }

        if (!Guid.TryParse(tenantIdHeader, out var tenantGuid))
        {
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "IntegrationTenantIdInvalid"), HttpContext.TraceIdentifier));
        }

        var tenantId = new TenantId(tenantGuid);

        if (!await _apiKeyValidation.ValidateAsync(tenantId, apiKey, "approval:read", cancellationToken))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyInvalid"), HttpContext.TraceIdentifier));
        }

        var instance = await _queryService.GetInstanceByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalInstanceNotFoundShort"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            InstanceId = instance.Id,
            Status = instance.Status,
            instance.BusinessKey,
            StartedAt = instance.StartedAt,
            EndedAt = instance.EndedAt
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>取消审批</summary>
    [HttpPost("{instanceId:long}/cancel")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-Tenant-Id")] string? tenantIdHeader,
        long instanceId,
        [FromBody] IntegrationCancelRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyRequired"), HttpContext.TraceIdentifier));
        }

        if (!Guid.TryParse(tenantIdHeader, out var tenantGuid))
        {
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "IntegrationTenantIdInvalid"), HttpContext.TraceIdentifier));
        }

        var tenantId = new TenantId(tenantGuid);

        if (!await _apiKeyValidation.ValidateAsync(tenantId, apiKey, "approval:write", cancellationToken))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyInvalid"), HttpContext.TraceIdentifier));
        }

        await _commandService.CancelInstanceAsync(tenantId, instanceId, request?.CancelledByUserId ?? 0, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed record IntegrationStartApprovalRequest(
    long FlowDefinitionId,
    string BusinessKey,
    string Title,
    long InitiatorUserId,
    string? FormData,
    string? ExtraData);

public sealed record IntegrationCancelRequest(long CancelledByUserId);
