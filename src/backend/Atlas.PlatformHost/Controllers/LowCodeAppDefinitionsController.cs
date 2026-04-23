using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 低代码应用定义控制器（M01，**设计态前缀** <c>/api/v1/lowcode/apps</c>）。
///
/// 强约束（PLAN.md §1.3）：
/// - 设计态写操作必须经此 controller，不允许混入运行时前缀。
/// - 所有写接口走 IAuditWriter（在 Service 层落审计）。
/// - PagedRequest 默认 pageSize=20，符合前端"远程检索默认 20 条"约束。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps")]
public sealed class LowCodeAppDefinitionsController : ControllerBase
{
    private readonly IAppDefinitionQueryService _query;
    private readonly IAppDefinitionCommandService _command;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppDefinitionsController(
        IAppDefinitionQueryService query,
        IAppDefinitionCommandService command,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUser)
    {
        _query = query;
        _command = command;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    /// <summary>分页查询应用列表。</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppDefinitionListItem>>>> List(
        [FromQuery] PagedRequest request,
        [FromQuery] string? status,
        [FromQuery] string? workspaceId,
        [FromQuery] string? folderId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _query.QueryAsync(request, tenantId, status, workspaceId, folderId, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppDefinitionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用详情（不含完整 schema JSON——通过 <c>GET /{id}/schema</c>）。</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppDefinitionDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _query.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");
        return Ok(ApiResponse<AppDefinitionDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>创建应用。</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] AppDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _command.CreateAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>更新应用元数据（不含 schema）。</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, [FromBody] AppDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.UpdateMetadataAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>删除应用。</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.DeleteAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>获取草稿（含完整 AppSchema JSON）。</summary>
    [HttpGet("{id:long}/draft")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppDraftResponse>>> GetDraft(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var draft = await _query.GetDraftAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");
        return Ok(ApiResponse<AppDraftResponse>.Ok(draft, HttpContext.TraceIdentifier));
    }

    /// <summary>替换草稿（手动保存）。</summary>
    [HttpPost("{id:long}/draft")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ReplaceDraft(long id, [FromBody] AppDraftReplaceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.ReplaceDraftAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>autosave 增量保存（M04 画布去抖 30s 自动调用，PLAN.md §M04 S04-1）。</summary>
    [HttpPost("{id:long}/autosave")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> AutoSave(long id, [FromBody] AppDraftAutoSaveRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.AutoSaveDraftAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>创建版本快照（M14 完整 diff/rollback 基于此聚合）。</summary>
    [HttpPost("{id:long}/snapshot")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> CreateSnapshot(long id, [FromBody] AppVersionSnapshotRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var versionId = await _command.CreateVersionSnapshotAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { versionId = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用版本归档列表（M14 完整使用）。</summary>
    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppVersionArchiveListItem>>>> ListVersions(
        long id,
        [FromQuery] bool includeSystemSnapshot = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _query.ListVersionsAsync(tenantId, id, includeSystemSnapshot, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppVersionArchiveListItem>>.Ok(list, HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用完整 Schema 快照（含 pages / variables / contentParams）。</summary>
    [HttpGet("{id:long}/schema")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppSchemaSnapshotDto>>> GetSchemaSnapshot(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var snapshot = await _query.GetSchemaSnapshotAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");
        return Ok(ApiResponse<AppSchemaSnapshotDto>.Ok(snapshot, HttpContext.TraceIdentifier));
    }
}
