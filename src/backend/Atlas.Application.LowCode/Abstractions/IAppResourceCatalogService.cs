using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 应用资源聚合（M07 S07-3）。"投射模式"：UI Builder 不引入新资源类型，
/// 由各资源域提供数据；本服务仅做轻量聚合查询（id/name/updatedAt），按 keyword + 类型过滤，
/// 默认 pageSize=20（与 AGENTS.md 前端规范"远程检索默认 20 条"一致）。
///
/// 当前已聚合资源类型（PLAN.md §M07 C07-5）：
///  - workflow（DagWorkflow + AiWorkflowDefinition）
///  - chatflow（DagWorkflow flow_mode=ChatFlow）
///  - database（AiDatabase）
///  - knowledge（KnowledgeBase）
///  - plugin（LowCodePluginDefinition + AiPlugin）
///  - prompt-template（AppPromptTemplate）
///  - variable（AppVariable）
///  - trigger（LowCodeTrigger）
/// </summary>
public interface IAppResourceCatalogService
{
    Task<AppResourceCatalogDto> SearchAsync(TenantId tenantId, AppResourceQuery query, CancellationToken cancellationToken);
}
