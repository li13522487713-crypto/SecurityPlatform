using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批抄送记录控制器（我的抄送、标记已读等）
/// </summary>
[ApiController]
[Route("api/v1/approval/copy-records")]
[Authorize]
public sealed class ApprovalCopyRecordsController : ControllerBase
{
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ApprovalCopyRecordsController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _currentUserAccessor = currentUserAccessor;
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
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetMyCopyRecordsAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request,
            isRead,
            cancellationToken);
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
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.MarkCopyRecordAsReadAsync(
            currentUser.TenantId,
            copyRecordId,
            currentUser.UserId,
            cancellationToken);
        return ApiResponse<string>.Ok("已标记为已读", HttpContext.TraceIdentifier);
    }
}
