using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 运行时 Schema 拉取（M08 S08-1，**运行时 runtime 前缀** /api/runtime/apps/{appId}/schema）。
///
/// 强约束（PLAN.md §1.3 #1）：
/// - 运行时只读端点严禁混入设计态写操作；写操作必须经 AppHost 设计态控制器（/api/v1/lowcode/*）。
/// </summary>
[ApiController]
[Route("api/runtime/apps/{appId:long}")]
[Authorize]
public sealed class RuntimeSchemaController : ControllerBase
{
    private readonly IAppDefinitionQueryService _query;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILowCodeRendererCapabilityService _capability;

    public RuntimeSchemaController(IAppDefinitionQueryService query, ITenantProvider tenantProvider, ILowCodeRendererCapabilityService capability)
    {
        _query = query;
        _tenantProvider = tenantProvider;
        _capability = capability;
    }

    /// <summary>查询渲染器能力差异化（M15 S15-2）。</summary>
    [HttpGet("/api/runtime/renderers/{renderer}/capability")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<RendererCapabilityDto>> GetRendererCapability(string renderer)
    {
        return Ok(ApiResponse<RendererCapabilityDto>.Ok(_capability.GetCapability(renderer), HttpContext.TraceIdentifier));
    }

    /// <summary>获取应用当前生效 Schema 快照（含 pages / variables / contentParams）。</summary>
    [HttpGet("schema")]
    public async Task<ActionResult<ApiResponse<AppSchemaSnapshotDto>>> GetSchema(
        long appId,
        [FromQuery] string? renderer,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var snapshot = await _query.GetSchemaSnapshotAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        // M15 已接入：把渲染器能力差异化通过 X-Atlas-Lowcode-Capability 响应头暴露给前端 RuntimeRenderer 使用。
        if (!string.IsNullOrWhiteSpace(renderer))
        {
            var cap = _capability.GetCapability(renderer);
            Response.Headers["X-Atlas-Lowcode-Renderer"] = renderer;
            Response.Headers["X-Atlas-Lowcode-Unsupported"] = string.Join(",", cap.UnsupportedComponentTypes);
        }
        return Ok(ApiResponse<AppSchemaSnapshotDto>.Ok(snapshot, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取指定版本的 Schema 快照（M08 → M14 完整支持）。
    /// 返回 app_version_archive.schema_snapshot_json 不可变副本 + resource_snapshot_json，供运行时按版本回看 / 灰度回滚预览。
    /// </summary>
    [HttpGet("versions/{versionId:long}/schema")]
    public async Task<ActionResult<ApiResponse<AppVersionedSchemaSnapshotDto>>> GetVersionSchema(
        long appId,
        long versionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var snapshot = await _query.GetVersionSchemaSnapshotAsync(tenantId, appId, versionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"版本不存在：app={appId} version={versionId}");
        return Ok(ApiResponse<AppVersionedSchemaSnapshotDto>.Ok(snapshot, HttpContext.TraceIdentifier));
    }
}
