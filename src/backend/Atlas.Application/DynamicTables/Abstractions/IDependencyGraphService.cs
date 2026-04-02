using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// 依赖图查询服务（T02-17 ~ T02-20）—— 查询表与视图/函数/流程之间的依赖关系。
/// </summary>
public interface IDependencyGraphService
{
    Task<DependencyGraphResult> GetDependenciesAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);
}
