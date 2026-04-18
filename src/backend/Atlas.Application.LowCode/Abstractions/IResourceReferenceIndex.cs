using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 资源引用增量索引（M14 S14-4）。
///
/// 当应用 schema 变更（autosave / replace-draft / page schema replace / snapshot）时自动调用 ReindexFromSchemaJsonAsync，
/// 解析 schema JSON 中的资源引用（workflowId / chatflowId / databaseId / knowledgeId / pluginId / promptTemplateId / triggerId / variable path）
/// 并通过 IResourceReferenceGuardService.ReindexForAppAsync 替换式更新索引表。
///
/// 设计：扫描器只看 JSON 字符串关键字（'workflowId': '...' 等），不依赖前端 zod 解析；性能与跨版本兼容性最优。
/// </summary>
public interface IResourceReferenceIndex
{
    Task ReindexFromSchemaJsonAsync(TenantId tenantId, long appId, string schemaJson, CancellationToken cancellationToken);
}
