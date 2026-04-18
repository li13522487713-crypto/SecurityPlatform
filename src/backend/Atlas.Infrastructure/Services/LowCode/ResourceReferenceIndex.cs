using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 资源引用增量索引实现（M14 S14-4）。
///
/// 解析策略（容错优先，不强制 schema 版本）：
///  - 用 System.Text.Json.JsonDocument 递归扫描；
///  - 收集 props.workflowId / chatflowId / pluginId / promptTemplateId / triggerId / databaseInfoId / knowledgeId 等"约定字段"；
///  - 对 BindingSchema.path（变量路径）按 'page.|app.|system.' 起始抽取顶层 segment 作为 variable refresh 依据。
/// </summary>
public sealed class ResourceReferenceIndex : IResourceReferenceIndex
{
    private static readonly Dictionary<string, string> ResourceFieldMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["workflowId"] = "workflow",
        ["chatflowId"] = "chatflow",
        ["pluginId"] = "plugin",
        ["promptTemplateId"] = "prompt-template",
        ["triggerId"] = "trigger",
        ["databaseInfoId"] = "database",
        ["databaseId"] = "database",
        ["knowledgeBaseId"] = "knowledge",
        ["knowledgeId"] = "knowledge"
    };

    private static readonly Regex VariablePathRegex = new(@"\b(page|app|system)\.([a-zA-Z_][a-zA-Z0-9_-]*)\b", RegexOptions.Compiled);

    private readonly IResourceReferenceGuardService _guard;
    private readonly ILogger<ResourceReferenceIndex> _logger;

    public ResourceReferenceIndex(IResourceReferenceGuardService guard, ILogger<ResourceReferenceIndex> logger)
    {
        _guard = guard;
        _logger = logger;
    }

    public async Task ReindexFromSchemaJsonAsync(TenantId tenantId, long appId, string schemaJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            await _guard.ReindexForAppAsync(tenantId, appId, Array.Empty<AppResourceReferenceDto>(), cancellationToken);
            return;
        }

        var refs = new List<AppResourceReferenceDto>();
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            Walk(doc.RootElement, currentPath: string.Empty, currentPageId: null, currentComponentId: null, refs);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ResourceReferenceIndex: schema JSON 解析失败，appId={AppId}", appId);
            // JSON 解析失败时不更新索引；保留上次成功索引，避免误清空。
            return;
        }

        // 去重：(resourceType, resourceId, referencePath)
        var unique = refs
            .GroupBy(r => (r.ResourceType, r.ResourceId, r.ReferencePath))
            .Select(g => g.First())
            .ToList();

        await _guard.ReindexForAppAsync(tenantId, appId, unique, cancellationToken);
        _logger.LogDebug("ResourceReferenceIndex: appId={AppId} indexed {Count} references", appId, unique.Count);
    }

    private static void Walk(JsonElement node, string currentPath, string? currentPageId, string? currentComponentId, List<AppResourceReferenceDto> refs)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                // 维护 page/component 上下文，便于 referencePath 精确定位
                var pageId = currentPageId;
                var componentId = currentComponentId;
                if (node.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    if (currentPath.Contains("/pages") && !currentPath.Contains("/root")) pageId = idEl.GetString();
                    if (currentPath.Contains("/root") || currentPath.Contains("/children")) componentId = idEl.GetString();
                }

                foreach (var prop in node.EnumerateObject())
                {
                    var childPath = $"{currentPath}/{prop.Name}";

                    // 资源 ID 字段（字符串值）
                    if (prop.Value.ValueKind == JsonValueKind.String && ResourceFieldMapping.TryGetValue(prop.Name, out var resourceType))
                    {
                        var rid = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(rid))
                        {
                            refs.Add(new AppResourceReferenceDto(
                                Id: "0",
                                AppId: "0",
                                PageId: pageId,
                                ComponentId: componentId,
                                ResourceType: resourceType,
                                ResourceId: rid!,
                                ReferencePath: childPath,
                                ResourceVersion: null,
                                CreatedAt: DateTimeOffset.UtcNow));
                        }
                    }
                    // BindingSchema.path（变量路径）
                    else if (string.Equals(prop.Name, "path", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var pathStr = prop.Value.GetString() ?? string.Empty;
                        var match = VariablePathRegex.Match(pathStr);
                        if (match.Success)
                        {
                            refs.Add(new AppResourceReferenceDto(
                                Id: "0",
                                AppId: "0",
                                PageId: pageId,
                                ComponentId: componentId,
                                ResourceType: "variable",
                                ResourceId: $"{match.Groups[1].Value}.{match.Groups[2].Value}",
                                ReferencePath: childPath,
                                ResourceVersion: null,
                                CreatedAt: DateTimeOffset.UtcNow));
                        }
                    }

                    Walk(prop.Value, childPath, pageId, componentId, refs);
                }
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in node.EnumerateArray())
                {
                    Walk(item, $"{currentPath}[{i}]", currentPageId, currentComponentId, refs);
                    i++;
                }
                break;
            // 其它值类型（string/number/bool/null）：上层已扫描（仅资源 ID / path 关心字符串）
        }
    }
}
