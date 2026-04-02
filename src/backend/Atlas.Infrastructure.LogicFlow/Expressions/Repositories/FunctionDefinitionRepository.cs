using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;
using SqlSugar;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Repositories;

public sealed class FunctionDefinitionRepository : IFunctionDefinitionRepository
{
    private readonly ISqlSugarClient _db;

    public FunctionDefinitionRepository(ISqlSugarClient db) => _db = db;

    public async Task<FunctionDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<FunctionDefinition>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);

    public async Task<FunctionDefinition?> GetByNameAsync(string name, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<FunctionDefinition>()
            .FirstAsync(x => x.Name == name && x.TenantIdValue == tenantId.Value, cancellationToken);

    public async Task<PagedResult<FunctionDefinition>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        TenantId tenantId,
        string? keyword,
        int? category,
        CancellationToken cancellationToken)
    {
        var total = new RefAsync<int>();
        var query = _db.Queryable<FunctionDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || (x.DisplayName != null && x.DisplayName.Contains(keyword)));
        if (category.HasValue)
            query = query.Where(x => (int)x.Category == category.Value);
        var items = await query.OrderBy(x => x.SortOrder).OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        return new PagedResult<FunctionDefinition>(items, total.Value, pageIndex, pageSize);
    }

    public async Task<List<FunctionDefinition>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<FunctionDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<long> InsertAsync(FunctionDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(FunctionDefinition entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .IgnoreColumns(x => new { x.CreatedAt, x.CreatedBy })
            .ExecuteCommandAsync(cancellationToken);

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Deleteable<FunctionDefinition>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
}
