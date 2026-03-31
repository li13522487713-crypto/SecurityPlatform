using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TeamAgentRepository : RepositoryBase<TeamAgent>
{
    public TeamAgentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<TeamAgent> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamAgentMode? teamMode,
        TeamAgentStatus? status,
        string? capabilityTag,
        string? defaultEntrySkill,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(tenantId, keyword, teamMode, status, capabilityTag, defaultEntrySkill);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public Task<List<TeamAgent>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgent>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public async Task<TeamAgent?> FindByLegacySourceAsync(
        TenantId tenantId,
        string legacySourceType,
        string legacySourceId,
        CancellationToken cancellationToken)
        => await Db.Queryable<TeamAgent>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.LegacySourceType == legacySourceType
                && x.LegacySourceId == legacySourceId)
            .FirstAsync(cancellationToken);

    public Task<int> CountByCapabilityTagAsync(TenantId tenantId, string capabilityTag, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CapabilityTagsJson.Contains($"\"{capabilityTag}\""))
            .CountAsync(cancellationToken);

    private ISugarQueryable<TeamAgent> BuildQuery(
        TenantId tenantId,
        string? keyword,
        TeamAgentMode? teamMode,
        TeamAgentStatus? status,
        string? capabilityTag,
        string? defaultEntrySkill)
    {
        var query = Db.Queryable<TeamAgent>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || (x.Description != null && x.Description.Contains(normalized)));
        }

        if (teamMode.HasValue)
        {
            query = query.Where(x => x.TeamMode == teamMode.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(capabilityTag))
        {
            var normalizedTag = capabilityTag.Trim();
            query = query.Where(x => x.CapabilityTagsJson.Contains($"\"{normalizedTag}\""));
        }

        if (!string.IsNullOrWhiteSpace(defaultEntrySkill))
        {
            var normalizedSkill = defaultEntrySkill.Trim();
            query = query.Where(x => x.DefaultEntrySkill == normalizedSkill);
        }

        return query;
    }
}

public sealed class TeamAgentPublicationRepository : RepositoryBase<TeamAgentPublication>
{
    public TeamAgentPublicationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentPublication>> GetByTeamAgentIdAsync(TenantId tenantId, long teamAgentId, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public async Task<int> GetLatestVersionAsync(TenantId tenantId, long teamAgentId, CancellationToken cancellationToken)
    {
        var items = await Db.Queryable<TeamAgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);
        return items.Count == 0 ? 0 : items[0].Version;
    }

    public async Task<int> DeactivateActiveByTeamAgentIdAsync(TenantId tenantId, long teamAgentId, CancellationToken cancellationToken)
    {
        var items = await Db.Queryable<TeamAgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.Deactivate();
            await Db.Updateable(item)
                .Where(x => x.TenantIdValue == item.TenantIdValue && x.Id == item.Id)
                .ExecuteCommandAsync(cancellationToken);
        }
        return items.Count;
    }
}

public sealed class TeamAgentTemplateRepository : RepositoryBase<TeamAgentTemplate>
{
    public TeamAgentTemplateRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentTemplate>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.IsSystem, OrderByType.Desc)
            .OrderBy(x => x.Name, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public async Task<TeamAgentTemplate?> FindByKeyAsync(TenantId tenantId, string key, CancellationToken cancellationToken)
        => await Db.Queryable<TeamAgentTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Key == key)
            .FirstAsync(cancellationToken);
}

public sealed class TeamAgentTemplateMemberRepository : RepositoryBase<TeamAgentTemplateMember>
{
    public TeamAgentTemplateMemberRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentTemplateMember>> GetByTemplateIdsAsync(TenantId tenantId, IReadOnlyList<long> templateIds, CancellationToken cancellationToken)
    {
        if (templateIds.Count == 0)
        {
            return Task.FromResult(new List<TeamAgentTemplateMember>());
        }

        var ids = templateIds.Distinct().ToArray();
        return Db.Queryable<TeamAgentTemplateMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.TemplateId))
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }
}

public sealed class TeamAgentConversationRepository : RepositoryBase<TeamAgentConversation>
{
    public TeamAgentConversationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<TeamAgentConversation> Items, long Total)> GetPagedByTeamAgentAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<TeamAgentConversation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId && x.UserId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastMessageAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public Task<List<TeamAgentConversation>> GetRecentAsync(TenantId tenantId, int take, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentConversation>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.LastMessageAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public sealed class TeamAgentMessageRepository : RepositoryBase<TeamAgentMessage>
{
    public TeamAgentMessageRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentMessage>> GetAllByConversationAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task<int> DeleteByConversationAsync(TenantId tenantId, long conversationId, CancellationToken cancellationToken)
        => Db.Deleteable<TeamAgentMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConversationId == conversationId)
            .ExecuteCommandAsync(cancellationToken);
}

public sealed class TeamAgentExecutionRepository : RepositoryBase<TeamAgentExecution>
{
    public TeamAgentExecutionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentExecution>> GetRecentAsync(TenantId tenantId, int take, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountRecentCompletedAsync(TenantId tenantId, DateTime sinceUtc, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.CreatedAt >= sinceUtc
                && (x.Status == TeamAgentExecutionStatus.Completed || x.Status == TeamAgentExecutionStatus.Running))
            .CountAsync(cancellationToken);
}

public sealed class TeamAgentExecutionStepRepository : RepositoryBase<TeamAgentExecutionStepEntity>
{
    public TeamAgentExecutionStepRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentExecutionStepEntity>> GetByExecutionIdAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentExecutionStepEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId)
            .OrderBy(x => x.StartedAt, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task<int> DeleteByExecutionIdAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
        => Db.Deleteable<TeamAgentExecutionStepEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId)
            .ExecuteCommandAsync(cancellationToken);

    public Task AddRangeAsync(IReadOnlyList<TeamAgentExecutionStepEntity> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(entities.ToArray()).ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class TeamAgentSchemaDraftRepository : RepositoryBase<TeamAgentSchemaDraft>
{
    public TeamAgentSchemaDraftRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<TeamAgentSchemaDraft?> FindByTeamAgentAndIdAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        CancellationToken cancellationToken)
        => await Db.Queryable<TeamAgentSchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId && x.Id == draftId)
            .FirstAsync(cancellationToken);

    public Task<List<TeamAgentSchemaDraft>> GetByTeamAgentAsync(TenantId tenantId, long teamAgentId, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentSchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamAgentId == teamAgentId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public Task<List<TeamAgentSchemaDraft>> GetRecentAsync(TenantId tenantId, int take, CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentSchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public sealed class TeamAgentSchemaDraftExecutionAuditRepository : RepositoryBase<TeamAgentSchemaDraftExecutionAudit>
{
    public TeamAgentSchemaDraftExecutionAuditRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<TeamAgentSchemaDraftExecutionAudit>> GetByDraftIdAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken)
        => Db.Queryable<TeamAgentSchemaDraftExecutionAudit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DraftId == draftId)
            .OrderBy(x => x.Sequence, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task<int> DeleteByDraftIdAsync(TenantId tenantId, long draftId, CancellationToken cancellationToken)
        => Db.Deleteable<TeamAgentSchemaDraftExecutionAudit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DraftId == draftId)
            .ExecuteCommandAsync(cancellationToken);

    public Task AddRangeAsync(IReadOnlyList<TeamAgentSchemaDraftExecutionAudit> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(entities.ToArray()).ExecuteCommandAsync(cancellationToken);
    }
}
