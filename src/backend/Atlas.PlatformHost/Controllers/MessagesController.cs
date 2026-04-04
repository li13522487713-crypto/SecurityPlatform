using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MessagesController(
        IMessageService messageService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _messageService = messageService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    // ─── 消息模板 ───

    [HttpGet("templates")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<MessageTemplateListItem>>>> GetTemplates(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _messageService.QueryTemplatesAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<MessageTemplateListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("templates/{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<MessageTemplateDetail?>>> GetTemplate(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _messageService.GetTemplateByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<MessageTemplateDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost("templates")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTemplate(
        [FromBody] MessageTemplateCreateRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _messageService.CreateTemplateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("templates/{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTemplate(
        long id, [FromBody] MessageTemplateUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _messageService.UpdateTemplateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("templates/{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _messageService.DeleteTemplateAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    // ─── 发送记录 ───

    [HttpGet("records")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<MessageRecordListItem>>>> GetRecords(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _messageService.QueryRecordsAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<MessageRecordListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ─── 发送消息 ───

    [HttpPost("send")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Send(
        [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _messageService.SendMessageAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Status = "Sent" }, HttpContext.TraceIdentifier));
    }

    // ─── 渠道配置 ───

    [HttpGet("channels")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChannelConfigItem>>>> GetChannelConfigs(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var configs = await _messageService.GetChannelConfigsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChannelConfigItem>>.Ok(configs, HttpContext.TraceIdentifier));
    }

    [HttpPut("channels/{channel}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateChannelConfig(
        string channel, [FromBody] ChannelConfigUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _messageService.UpdateChannelConfigAsync(tenantId, channel, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Channel = channel }, HttpContext.TraceIdentifier));
    }
}
