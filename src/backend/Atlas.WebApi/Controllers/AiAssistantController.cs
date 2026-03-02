using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/ai")]
public sealed class AiAssistantController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ITenantProvider _tenantProvider;

    public AiAssistantController(IAiService aiService, ITenantProvider tenantProvider)
    {
        _aiService = aiService;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// AI 生成表单 Schema
    /// </summary>
    [HttpPost("generate-form")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<AiFormGenerateResponse>>> GenerateForm(
        [FromBody] AiFormGenerateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiService.GenerateFormAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<AiFormGenerateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// AI 生成 SQL
    /// </summary>
    [HttpPost("generate-sql")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<AiSqlGenerateResponse>>> GenerateSql(
        [FromBody] AiSqlGenerateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiService.GenerateSqlAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<AiSqlGenerateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// AI 建议工作流
    /// </summary>
    [HttpPost("suggest-workflow")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<AiWorkflowSuggestResponse>>> SuggestWorkflow(
        [FromBody] AiWorkflowSuggestRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiService.SuggestWorkflowAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<AiWorkflowSuggestResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// AI 聊天
    /// </summary>
    [HttpPost("chat")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> Chat(
        [FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _aiService.ChatAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<AiChatResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// AI 聊天（SSE 流式）
    /// </summary>
    [HttpPost("chat/stream")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task ChatStream(
        [FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();

        await foreach (var chunk in _aiService.ChatStreamAsync(tenantId, request, cancellationToken))
        {
            await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
