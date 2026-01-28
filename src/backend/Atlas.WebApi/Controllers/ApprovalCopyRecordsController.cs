using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批抄送记录控制器（我的抄送、标记已读等）
/// </summary>
[ApiController]
[Route("api/approval/copy-records")]
[Authorize]
public sealed class ApprovalCopyRecordsController : ControllerBase
{
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;

    public ApprovalCopyRecordsController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// 获取我的抄送记录
    /// </summary>
    [HttpGet("my-copies")]
    public async Task<ApiResponse<PagedResult<ApprovalCopyRecordResponse>>> GetMyCopyRecordsAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetMyCopyRecordsAsync(tenantId, userId, request, isRead, cancellationToken);
        return ApiResponse<PagedResult<ApprovalCopyRecordResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 标记抄送记录为已读
    /// </summary>
    [HttpPost("{copyRecordId}/mark-read")]
    public async Task<ApiResponse<string>> MarkAsReadAsync(
        long copyRecordId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

        await _commandService.MarkCopyRecordAsReadAsync(tenantId, copyRecordId, userId, cancellationToken);
        return ApiResponse<string>.Ok("已标记为已读", HttpContext.TraceIdentifier);
    }
}
