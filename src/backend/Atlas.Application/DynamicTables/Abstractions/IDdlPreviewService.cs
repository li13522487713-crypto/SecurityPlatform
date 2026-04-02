using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// DDL 预览服务 —— 生成 up script / down hint / warning list，
/// 并按能力矩阵裁剪不支持的 DDL 片段（T02-12, T02-13, T02-30, T02-34）。
/// </summary>
public interface IDdlPreviewService
{
    Task<DdlPreviewResult> PreviewAsync(
        TenantId tenantId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken);
}
