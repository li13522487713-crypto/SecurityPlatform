using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Aggregates metadata references across forms, pages, and approval flows for a given dynamic table.
/// </summary>
public sealed class MetadataLinkQueryService : IMetadataLinkQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;

    public MetadataLinkQueryService(ISqlSugarClient db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<EntityReferenceResult> GetEntityReferencesAsync(
        string tableKey,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();

        // Run all three queries in parallel to avoid multiple sequential DB round-trips.
        var formTask = _db.Queryable<FormDefinition>()
            .Where(f => f.TenantIdValue == tenantId.Value && f.DataTableKey == tableKey)
            .Select(f => new FormDefinitionRef(f.Id, f.Name, f.Description, f.Category, f.Status.ToString()))
            .ToListAsync(cancellationToken);

        var pageTask = _db.Queryable<LowCodePage>()
            .Where(p => p.TenantIdValue == tenantId.Value && p.DataTableKey == tableKey)
            .Select(p => new LowCodePageRef(p.Id, p.PageKey, p.Name, p.AppId))
            .ToListAsync(cancellationToken);

        var tableTask = _db.Queryable<DynamicTable>()
            .Where(t => t.TenantIdValue == tenantId.Value && t.TableKey == tableKey)
            .Select(t => new { t.ApprovalFlowDefinitionId })
            .FirstAsync(cancellationToken);

        await Task.WhenAll(formTask, pageTask, tableTask);

        var forms = (IReadOnlyList<FormDefinitionRef>)formTask.Result;
        var pages = (IReadOnlyList<LowCodePageRef>)pageTask.Result;
        var tableRow = tableTask.Result;

        ApprovalFlowRef? approvalFlow = null;
        if (tableRow?.ApprovalFlowDefinitionId is { } flowId)
        {
            var flow = await _db.Queryable<ApprovalFlowDefinition>()
                .Where(a => a.TenantIdValue == tenantId.Value && a.Id == flowId)
                .Select(a => new ApprovalFlowRef(a.Id, a.Name, a.Status.ToString()))
                .FirstAsync(cancellationToken);
            approvalFlow = flow;
        }

        return new EntityReferenceResult(tableKey, forms, pages, approvalFlow);
    }
}
