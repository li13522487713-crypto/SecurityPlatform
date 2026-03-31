using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicViews.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Aggregates metadata references across forms, pages, approval flows, relations and dynamic views for a given dynamic table.
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
            .Select(t => new { t.Id, t.ApprovalFlowDefinitionId })
            .FirstAsync(cancellationToken);

        await Task.WhenAll(formTask, pageTask, tableTask);

        var forms = (IReadOnlyList<FormDefinitionRef>)formTask.Result;
        var pages = (IReadOnlyList<LowCodePageRef>)pageTask.Result;
        var tableRow = tableTask.Result;

        ApprovalFlowRef? approvalFlow = null;
        if (tableRow?.ApprovalFlowDefinitionId is { } flowId)
        {
            approvalFlow = await _db.Queryable<ApprovalFlowDefinition>()
                .Where(a => a.TenantIdValue == tenantId.Value && a.Id == flowId)
                .Select(a => new ApprovalFlowRef(a.Id, a.Name, a.Status.ToString()))
                .FirstAsync(cancellationToken);
        }

        IReadOnlyList<DynamicRelationRef> relations = Array.Empty<DynamicRelationRef>();
        if (tableRow is not null)
        {
            relations = await _db.Queryable<DynamicRelation>()
                .Where(r => r.TenantIdValue == tenantId.Value && (r.TableId == tableRow.Id || r.RelatedTableKey == tableKey))
                .Select(r => new DynamicRelationRef(r.Id, r.RelatedTableKey, r.SourceField, r.TargetField))
                .ToListAsync(cancellationToken);
        }

        var marker = $"\"tableKey\":\"{tableKey}\"";
        var views = await _db.Queryable<DynamicViewDefinition>()
            .Where(v => v.TenantIdValue == tenantId.Value && (v.DefinitionJson.Contains(marker) || v.DraftDefinitionJson.Contains(marker)))
            .Select(v => new DynamicViewRef(v.ViewKey, v.Name))
            .ToListAsync(cancellationToken);

        return new EntityReferenceResult(
            tableKey,
            forms,
            pages,
            approvalFlow,
            relations,
            views,
            Array.Empty<TransformJobRef>());
    }
}
