using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 应用资源聚合实现（M07 S07-3）。
/// "投射模式"：直接读各资源域的实体表（避免依赖各域 Service，减少耦合）；
/// 仅返回 id/name/updatedAt + 描述四段最小元信息。
/// </summary>
public sealed class AppResourceCatalogService : IAppResourceCatalogService
{
    private static readonly IReadOnlyList<string> AllSupportedTypes = new[]
    {
        "workflow",
        "chatflow",
        "database",
        "knowledge",
        "plugin",
        "prompt-template",
        "variable",
        "trigger"
    };

    private readonly ISqlSugarClient _db;

    public AppResourceCatalogService(ISqlSugarClient db) => _db = db;

    public async Task<AppResourceCatalogDto> SearchAsync(TenantId tenantId, AppResourceQuery query, CancellationToken cancellationToken)
    {
        var requested = string.IsNullOrWhiteSpace(query.Types)
            ? AllSupportedTypes
            : query.Types!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Where(AllSupportedTypes.Contains)
                .ToList();

        var pageIndex = query.PageIndex ?? 1;
        var pageSize = Math.Min(query.PageSize ?? 20, 200);
        var keyword = query.Keyword;

        var byType = new Dictionary<string, IReadOnlyList<AppResourceItem>>(StringComparer.OrdinalIgnoreCase);
        var total = 0;

        foreach (var type in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var list = type switch
            {
                "workflow" => await QueryWorkflowsAsync(tenantId, keyword, pageIndex, pageSize, includeChat: false, cancellationToken),
                "chatflow" => await QueryWorkflowsAsync(tenantId, keyword, pageIndex, pageSize, includeChat: true, cancellationToken),
                "database" => await QueryDatabasesAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                "knowledge" => await QueryKnowledgeAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                "plugin" => await QueryPluginsAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                "prompt-template" => await QueryPromptTemplatesAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                "variable" => await QueryVariablesAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                "trigger" => await QueryTriggersAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken),
                _ => Array.Empty<AppResourceItem>() as IReadOnlyList<AppResourceItem>
            };
            byType[type] = list;
            total += list.Count;
        }

        return new AppResourceCatalogDto(byType, total);
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryWorkflowsAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, bool includeChat, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsDeleted == false);
        q = includeChat
            ? q.Where(x => x.Mode == WorkflowMode.ChatFlow)
            : q.Where(x => x.Mode == WorkflowMode.Standard);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.Name.Contains(keyword) || (x.Description != null && x.Description.Contains(keyword)));
        var rows = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem(includeChat ? "chatflow" : "workflow", r.Id.ToString(), r.Name, r.Description, new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero))).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryDatabasesAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AiDatabase>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Name.Contains(keyword));
        var rows = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem("database", r.Id.ToString(), r.Name, null, r.UpdatedAt.HasValue ? new DateTimeOffset(r.UpdatedAt.Value, TimeSpan.Zero) : null)).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryKnowledgeAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<KnowledgeBase>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Name.Contains(keyword));
        var rows = await q.OrderBy(x => x.CreatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem("knowledge", r.Id.ToString(), r.Name, null, new DateTimeOffset(r.CreatedAt, TimeSpan.Zero))).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryPluginsAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        // 同时合并 LowCodePluginDefinition + AiPlugin 两类（前者为新插件市场，后者为既有 N10 节点的注册表）
        var lowCodePlugins = _db.Queryable<LowCodePluginDefinition>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) lowCodePlugins = lowCodePlugins.Where(x => x.Name.Contains(keyword) || x.PluginId.Contains(keyword));
        var lcList = await lowCodePlugins.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var lcItems = lcList.Select(r => new AppResourceItem("plugin", r.Id.ToString(), r.Name, r.Description, r.UpdatedAt));

        var aiPlugins = _db.Queryable<AiPlugin>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) aiPlugins = aiPlugins.Where(x => x.Name.Contains(keyword));
        var apList = await aiPlugins.OrderBy(x => x.CreatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var apItems = apList.Select(r => new AppResourceItem("plugin", $"ai:{r.Id}", r.Name, null, new DateTimeOffset(r.CreatedAt, TimeSpan.Zero)));

        return lcItems.Concat(apItems).Take(pageSize).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryPromptTemplatesAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppPromptTemplate>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        var rows = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem("prompt-template", r.Id.ToString(), r.Name, r.Description, r.UpdatedAt)).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryVariablesAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppVariable>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Code.Contains(keyword) || x.DisplayName.Contains(keyword));
        var rows = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem("variable", r.Id.ToString(), r.DisplayName, r.Description, r.UpdatedAt)).ToList();
    }

    private async Task<IReadOnlyList<AppResourceItem>> QueryTriggersAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<LowCodeTrigger>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Name.Contains(keyword) || x.TriggerId.Contains(keyword));
        var rows = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return rows.Select(r => new AppResourceItem("trigger", r.TriggerId, r.Name, $"kind={r.Kind}", r.UpdatedAt)).ToList();
    }
}
