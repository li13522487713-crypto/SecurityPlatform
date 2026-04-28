using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentTriggerService : IAgentTriggerService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AgentTriggerService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<AgentTriggerDto>> ListAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var entities = await _db.Queryable<AgentTrigger>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<AgentTriggerDto> CreateAsync(TenantId tenantId, long agentId, long createdBy, AgentTriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = new AgentTrigger(tenantId, agentId, request.Name, request.TriggerType, request.ConfigJson, request.IsEnabled, createdBy, _idGenerator.NextId());
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task UpdateAsync(TenantId tenantId, long agentId, long triggerId, AgentTriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var entity = await _db.Queryable<AgentTrigger>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId && x.Id == triggerId)
            .FirstAsync(cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AgentTriggerNotFound");
        entity.Update(request.Name, request.TriggerType, request.ConfigJson, request.IsEnabled);
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long agentId, long triggerId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        await _db.Deleteable<AgentTrigger>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId && x.Id == triggerId)
            .ExecuteCommandAsync(cancellationToken);
    }

    internal static AgentTriggerDto ToDto(AgentTrigger entity)
    {
        return new AgentTriggerDto(
            Id: entity.Id.ToString(),
            AgentId: entity.AgentId.ToString(),
            Name: entity.Name,
            TriggerType: entity.TriggerType,
            ConfigJson: entity.ConfigJson,
            IsEnabled: entity.IsEnabled,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}

public sealed class AgentCardService : IAgentCardService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AgentCardService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<AgentCardDto>> ListAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var entities = await _db.Queryable<AgentCard>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<AgentCardDto> CreateAsync(TenantId tenantId, long agentId, long createdBy, AgentCardUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = new AgentCard(tenantId, agentId, request.Name, request.CardType, request.SchemaJson, request.IsEnabled, createdBy, _idGenerator.NextId());
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task UpdateAsync(TenantId tenantId, long agentId, long cardId, AgentCardUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var entity = await _db.Queryable<AgentCard>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId && x.Id == cardId)
            .FirstAsync(cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AgentCardNotFound");
        entity.Update(request.Name, request.CardType, request.SchemaJson, request.IsEnabled);
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long agentId, long cardId, CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        await _db.Deleteable<AgentCard>()
            .Where(x => x.TenantIdValue == tenantValue && x.AgentId == agentId && x.Id == cardId)
            .ExecuteCommandAsync(cancellationToken);
    }

    internal static AgentCardDto ToDto(AgentCard entity)
    {
        return new AgentCardDto(
            Id: entity.Id.ToString(),
            AgentId: entity.AgentId.ToString(),
            Name: entity.Name,
            CardType: entity.CardType,
            SchemaJson: entity.SchemaJson,
            IsEnabled: entity.IsEnabled,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
