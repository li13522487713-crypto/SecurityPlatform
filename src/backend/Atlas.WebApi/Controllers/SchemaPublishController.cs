using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// Schema 发布快照 / 兼容性检查 / DDL 预览 / 依赖图 / 影响分析 API
/// T02-24 ~ T02-26, T02-36
/// </summary>
[ApiController]
[Route("api/v1/schema")]
public sealed class SchemaPublishController : ControllerBase
{
    private readonly ISchemaPublishSnapshotQueryService _snapshotQuery;
    private readonly ISchemaPublishSnapshotCommandService _snapshotCommand;
    private readonly ISchemaCompatibilityChecker _compatibilityChecker;
    private readonly IDdlPreviewService _ddlPreview;
    private readonly IDependencyGraphService _dependencyGraph;
    private readonly ISchemaImpactAnalysisService _impactAnalysis;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public SchemaPublishController(
        ISchemaPublishSnapshotQueryService snapshotQuery,
        ISchemaPublishSnapshotCommandService snapshotCommand,
        ISchemaCompatibilityChecker compatibilityChecker,
        IDdlPreviewService ddlPreview,
        IDependencyGraphService dependencyGraph,
        ISchemaImpactAnalysisService impactAnalysis,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _snapshotQuery = snapshotQuery;
        _snapshotCommand = snapshotCommand;
        _compatibilityChecker = compatibilityChecker;
        _ddlPreview = ddlPreview;
        _dependencyGraph = dependencyGraph;
        _impactAnalysis = impactAnalysis;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    // ─── T02-24: 发布快照 API ───

    [HttpGet("snapshots")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<SchemaPublishSnapshotListItem>>>> GetSnapshots(
        [FromQuery] string? tableKey,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _snapshotQuery.QueryAsync(tenantId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<SchemaPublishSnapshotListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("snapshots/{snapshotId:long}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaPublishSnapshotDetail?>>> GetSnapshot(
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _snapshotQuery.GetByIdAsync(tenantId, snapshotId, cancellationToken);
        return Ok(ApiResponse<SchemaPublishSnapshotDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("snapshots/latest")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaPublishSnapshotDetail?>>> GetLatestSnapshot(
        [FromQuery] string tableKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _snapshotQuery.GetLatestAsync(tenantId, tableKey, cancellationToken);
        return Ok(ApiResponse<SchemaPublishSnapshotDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("snapshots/diff")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaSnapshotDiffResult?>>> DiffSnapshots(
        [FromQuery] string tableKey,
        [FromQuery] int fromVersion,
        [FromQuery] int toVersion,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var diff = await _snapshotQuery.DiffAsync(tenantId, tableKey, fromVersion, toVersion, cancellationToken);
        return Ok(ApiResponse<SchemaSnapshotDiffResult?>.Ok(diff, HttpContext.TraceIdentifier));
    }

    [HttpPost("snapshots")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateSnapshot(
        [FromBody] SchemaPublishSnapshotCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _snapshotCommand.CreateSnapshotAsync(tenantId, currentUser.UserId, request, cancellationToken);

        await RecordAuditAsync(currentUser, "CREATE_SCHEMA_SNAPSHOT", request.TableKey, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), TableKey = request.TableKey }, HttpContext.TraceIdentifier));
    }

    // ─── T02-25: 兼容性检查 API ───

    [HttpPost("compatibility-check")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaCompatibilityResult>>> CheckCompatibility(
        [FromBody] SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _compatibilityChecker.CheckAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<SchemaCompatibilityResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ─── T02-26: DDL 预览 API ───

    [HttpPost("ddl-preview")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DdlPreviewResult>>> PreviewDdl(
        [FromBody] SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _ddlPreview.PreviewAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<DdlPreviewResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ─── 依赖图 API ───

    [HttpGet("dependencies/{tableKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DependencyGraphResult>>> GetDependencies(
        string tableKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _dependencyGraph.GetDependenciesAsync(tenantId, tableKey, cancellationToken);
        return Ok(ApiResponse<DependencyGraphResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    // ─── 影响分析 API ───

    [HttpGet("impact/{tableKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaImpactList>>> GetImpact(
        string tableKey,
        [FromQuery] string? removingFields,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        IReadOnlyList<string>? fields = string.IsNullOrWhiteSpace(removingFields)
            ? null
            : removingFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = await _impactAnalysis.AnalyzeAsync(tenantId, tableKey, fields, cancellationToken);
        return Ok(ApiResponse<SchemaImpactList>.Ok(result, HttpContext.TraceIdentifier));
    }

    private Task RecordAuditAsync(
        CurrentUserInfo currentUser,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var context = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        return _auditRecorder.RecordAsync(context, cancellationToken);
    }
}
