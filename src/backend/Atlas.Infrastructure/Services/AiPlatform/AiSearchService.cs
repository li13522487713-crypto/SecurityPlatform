using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiSearchService : IAiSearchService
{
    private readonly ISqlSugarClient _db;
    private readonly AiRecentEditRepository _recentEditRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AiSearchService(
        ISqlSugarClient db,
        AiRecentEditRepository recentEditRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _recentEditRepository = recentEditRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<AiSearchResponse> SearchAsync(
        TenantId tenantId,
        long userId,
        string? keyword,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = keyword?.Trim();
        var safeLimit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            var recent = await GetRecentEditsAsync(tenantId, userId, safeLimit, cancellationToken);
            return new AiSearchResponse([], recent);
        }

        var perSourceLimit = Math.Min(10, safeLimit);

        var agents = await _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("agent", x.Id, x.Name, x.Description, $"/ai/agents/{x.Id}/edit", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var knowledgeBases = await _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("knowledge-base", x.Id, x.Name, x.Description, $"/ai/knowledge-bases/{x.Id}", x.CreatedAt))
            .ToListAsync(cancellationToken);

        var databases = await _db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("database", x.Id, x.Name, x.Description, $"/ai/databases/{x.Id}", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var plugins = await _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("plugin", x.Id, x.Name, x.Description, $"/ai/plugins/{x.Id}", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var apps = await _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("app", x.Id, x.Name, x.Description, $"/ai/apps/{x.Id}/edit", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var prompts = await _db.Queryable<AiPromptTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword) || x.Content.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("prompt", x.Id, x.Name, x.Description, "/ai/prompts", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var marketProducts = await _db.Queryable<AiMarketplaceProduct>()
            .Where(x => x.TenantIdValue == tenantId.Value && (x.Name.Contains(normalizedKeyword) || x.Summary!.Contains(normalizedKeyword) || x.Description!.Contains(normalizedKeyword)))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perSourceLimit)
            .Select(x => new AiSearchResultItem("marketplace", x.Id, x.Name, x.Summary, $"/ai/marketplace/{x.Id}", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var allItems = agents
            .Concat(knowledgeBases)
            .Concat(databases)
            .Concat(plugins)
            .Concat(apps)
            .Concat(prompts)
            .Concat(marketProducts)
            .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
            .Take(safeLimit)
            .ToArray();

        var recentEdits = await GetRecentEditsAsync(tenantId, userId, Math.Min(10, safeLimit), cancellationToken);
        return new AiSearchResponse(allItems, recentEdits);
    }

    public async Task<IReadOnlyList<AiRecentEditItem>> GetRecentEditsAsync(
        TenantId tenantId,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        var records = await _recentEditRepository.GetRecentByUserAsync(tenantId, userId, safeLimit, cancellationToken);
        return records
            .Select(x => new AiRecentEditItem(
                x.Id,
                x.ResourceType,
                x.ResourceId,
                x.ResourceTitle,
                x.ResourcePath,
                x.UpdatedAt ?? x.CreatedAt))
            .ToArray();
    }

    public async Task<long> RecordRecentEditAsync(
        TenantId tenantId,
        long userId,
        AiRecentEditCreateRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _recentEditRepository.FindByResourceAsync(
            tenantId,
            userId,
            request.ResourceType.Trim(),
            request.ResourceId,
            cancellationToken);
        if (existing is not null)
        {
            existing.Refresh(request.Title.Trim(), request.Path.Trim());
            await _recentEditRepository.UpdateAsync(existing, cancellationToken);
            return existing.Id;
        }

        var record = new AiRecentEdit(
            tenantId,
            userId,
            request.ResourceType.Trim(),
            request.ResourceId,
            request.Title.Trim(),
            request.Path.Trim(),
            _idGeneratorAccessor.NextId());
        await _recentEditRepository.AddAsync(record, cancellationToken);
        return record.Id;
    }

    public async Task DeleteRecentEditAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken)
    {
        var record = await _recentEditRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("最近编辑记录不存在。", ErrorCodes.NotFound);
        if (record.UserId != userId)
        {
            throw new BusinessException("无权删除该记录。", ErrorCodes.Forbidden);
        }

        await _recentEditRepository.DeleteAsync(tenantId, id, cancellationToken);
    }
}
