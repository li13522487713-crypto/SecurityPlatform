using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/workspaces/{workspaceId}/tasks")]
[Authorize]
public sealed class WorkspaceTasksController : ControllerBase
{
    private readonly IWorkspaceTaskService _service;
    private readonly ITenantProvider _tenantProvider;

    public WorkspaceTasksController(IWorkspaceTaskService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceTaskItemDto>>>> List(
        string workspaceId,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var statusEnum = ParseStatus(status);
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _service.ListAsync(tenantId, workspaceId, statusEnum, type, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceTaskItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{taskId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<WorkspaceTaskDetailDto>>> GetById(
        string workspaceId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _service.GetAsync(tenantId, workspaceId, taskId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<WorkspaceTaskDetailDto>.Fail(
                ErrorCodes.NotFound,
                "Task not found.",
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<WorkspaceTaskDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    private static WorkspaceTaskStatus? ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return raw.ToLowerInvariant() switch
        {
            "pending" => WorkspaceTaskStatus.Pending,
            "running" => WorkspaceTaskStatus.Running,
            "succeeded" => WorkspaceTaskStatus.Succeeded,
            "failed" => WorkspaceTaskStatus.Failed,
            _ => null
        };
    }
}
