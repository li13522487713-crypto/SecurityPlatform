using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class ReportService : IReportService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public ReportService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<ReportDefinitionListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ReportDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x => x.Name.Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        var mapped = items.Select(e => new ReportDefinitionListItem(
            e.Id.ToString(), e.Name, e.Description, e.Category, e.Version,
            e.Status.ToString(), e.CreatedAt, e.UpdatedAt
        )).ToList();

        return new PagedResult<ReportDefinitionListItem>(mapped, total, request.PageIndex, request.PageSize);
    }

    public async Task<ReportDefinitionDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Queryable<ReportDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);

        if (e is null) return null;

        return new ReportDefinitionDetail(
            e.Id.ToString(), e.Name, e.Description, e.Category, e.ConfigJson,
            e.DataSourceJson, e.PrintTemplateJson, e.Version, e.Status.ToString(),
            e.CreatedAt, e.UpdatedAt, e.CreatedBy, e.UpdatedBy
        );
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, ReportDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;
        var entity = new ReportDefinition(tenantId, request.Name, request.Description, request.Category, request.ConfigJson, userId, id, now);

        if (!string.IsNullOrWhiteSpace(request.DataSourceJson))
        {
            entity.SetDataSource(request.DataSourceJson, userId, now);
        }

        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, ReportDefinitionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<ReportDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken)
            ?? throw new InvalidOperationException($"报表定义 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Update(request.Name, request.Description, request.Category, request.ConfigJson, userId, now);

        if (request.DataSourceJson != null)
            entity.SetDataSource(request.DataSourceJson, userId, now);
        if (request.PrintTemplateJson != null)
            entity.UpdatePrintTemplate(request.PrintTemplateJson, userId, now);

        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        await _db.Deleteable<ReportDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}
