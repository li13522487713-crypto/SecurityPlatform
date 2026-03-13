using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkspaceService : IAiWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiWorkspaceRepository _workspaceRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ISqlSugarClient _db;

    public AiWorkspaceService(
        AiWorkspaceRepository workspaceRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ISqlSugarClient db)
    {
        _workspaceRepository = workspaceRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _db = db;
    }

    public async Task<AiWorkspaceDto> GetCurrentAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByUserIdAsync(tenantId, userId, cancellationToken);
        if (workspace is null)
        {
            workspace = new AiWorkspace(
                tenantId,
                userId,
                "我的 AI 工作台",
                "light",
                "/ai/workspace",
                "[]",
                _idGeneratorAccessor.NextId());
            await _workspaceRepository.AddAsync(workspace, cancellationToken);
        }

        return MapWorkspace(workspace);
    }

    public async Task<AiWorkspaceDto> UpdateAsync(
        TenantId tenantId,
        long userId,
        AiWorkspaceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByUserIdAsync(tenantId, userId, cancellationToken);
        if (workspace is null)
        {
            workspace = new AiWorkspace(
                tenantId,
                userId,
                request.Name.Trim(),
                request.Theme.Trim(),
                request.LastVisitedPath.Trim(),
                SerializeFavoriteIds(request.FavoriteResourceIds),
                _idGeneratorAccessor.NextId());
            await _workspaceRepository.AddAsync(workspace, cancellationToken);
            return MapWorkspace(workspace);
        }

        workspace.Update(
            request.Name.Trim(),
            request.Theme.Trim(),
            request.LastVisitedPath.Trim(),
            SerializeFavoriteIds(request.FavoriteResourceIds));
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task<AiLibraryPagedResult> GetLibraryAsync(
        TenantId tenantId,
        AiLibraryQueryRequest request,
        CancellationToken cancellationToken)
    {
        var keyword = request.Keyword?.Trim();
        var resourceType = request.ResourceType?.Trim().ToLowerInvariant();
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var perTypeLimit = 50;

        var agents = await _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword) || x.Description!.Contains(keyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .Select(x => new AiLibraryItem("agent", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, $"/ai/agents/{x.Id}/edit"))
            .ToListAsync(cancellationToken);

        var knowledgeBases = await _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword) || x.Description!.Contains(keyword)))
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .Select(x => new AiLibraryItem("knowledge-base", x.Id, x.Name, x.Description, x.CreatedAt, $"/ai/knowledge-bases/{x.Id}"))
            .ToListAsync(cancellationToken);

        var workflows = await _db.Queryable<AiWorkflowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword) || x.Description!.Contains(keyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .Select(x => new AiLibraryItem("workflow", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, $"/ai/workflows/{x.Id}/edit"))
            .ToListAsync(cancellationToken);

        var apps = await _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword) || x.Description!.Contains(keyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .Select(x => new AiLibraryItem("app", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, $"/ai/apps/{x.Id}/edit"))
            .ToListAsync(cancellationToken);

        var prompts = await _db.Queryable<AiPromptTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword) || x.Description!.Contains(keyword) || x.Content.Contains(keyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .Select(x => new AiLibraryItem("prompt", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, "/ai/prompts"))
            .ToListAsync(cancellationToken);

        var allItems = agents
            .Concat(knowledgeBases)
            .Concat(workflows)
            .Concat(apps)
            .Concat(prompts);

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            allItems = allItems.Where(x => string.Equals(x.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = allItems.OrderByDescending(x => x.UpdatedAt).ToList();
        var total = ordered.Count;
        var pagedItems = ordered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
        return new AiLibraryPagedResult(pagedItems, total, pageIndex, pageSize);
    }

    private static AiWorkspaceDto MapWorkspace(AiWorkspace workspace)
        => new(
            workspace.Id,
            workspace.Name,
            workspace.Theme,
            workspace.LastVisitedPath,
            ParseFavoriteIds(workspace.FavoriteResourceIdsJson),
            workspace.CreatedAt,
            workspace.UpdatedAt);

    private static long[] ParseFavoriteIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<long[]>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeFavoriteIds(long[] favoriteIds)
    {
        var normalized = favoriteIds
            .Where(x => x > 0)
            .Distinct()
            .Take(200)
            .ToArray();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }
}
