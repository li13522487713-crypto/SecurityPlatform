using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicViews.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicDeleteCheckService : IDynamicDeleteCheckService
{
    private readonly ISqlSugarClient _db;
    private readonly IMetadataLinkQueryService _metadataLinkQueryService;
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicViewRepository _viewRepository;

    public DynamicDeleteCheckService(
        ISqlSugarClient db,
        IMetadataLinkQueryService metadataLinkQueryService,
        IDynamicTableRepository tableRepository,
        IDynamicViewRepository viewRepository)
    {
        _db = db;
        _metadataLinkQueryService = metadataLinkQueryService;
        _tableRepository = tableRepository;
        _viewRepository = viewRepository;
    }

    public async Task<DeleteCheckResultDto> CheckTableDeleteAsync(TenantId tenantId, long? appId, string tableKey, CancellationToken cancellationToken)
    {
        var blockers = new List<DeleteCheckBlockerDto>();

        var metadataRefs = await _metadataLinkQueryService.GetEntityReferencesAsync(tableKey, cancellationToken);
        blockers.AddRange(metadataRefs.FormDefinitions.Select(form => new DeleteCheckBlockerDto("form", form.Id.ToString(), form.Name, $"/apps/{appId}/forms/{form.Id}/designer")));
        blockers.AddRange(metadataRefs.LowCodePages.Select(page => new DeleteCheckBlockerDto("page", page.Id.ToString(), page.Name, $"/apps/{page.AppId}/pages")));
        if (metadataRefs.BoundApprovalFlow is not null)
        {
            blockers.Add(new DeleteCheckBlockerDto(
                "approval",
                metadataRefs.BoundApprovalFlow.Id.ToString(),
                metadataRefs.BoundApprovalFlow.Name,
                $"/apps/{appId}/flows"));
        }

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, appId, cancellationToken);
        if (table is not null)
        {
            var sourceRelations = await _db.Queryable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == table.Id)
                .ToListAsync(cancellationToken);
            blockers.AddRange(sourceRelations.Select(relation => new DeleteCheckBlockerDto(
                "relation",
                relation.Id.ToString(),
                $"{tableKey}.{relation.SourceField} -> {relation.RelatedTableKey}.{relation.TargetField}")));
        }

        var targetRelations = await _db.Queryable<DynamicRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RelatedTableKey == tableKey)
            .ToListAsync(cancellationToken);
        blockers.AddRange(targetRelations.Select(relation => new DeleteCheckBlockerDto(
            "relation",
            relation.Id.ToString(),
            $"Referenced by relation {relation.RelatedTableKey}.{relation.TargetField}")));

        var viewRefs = await _viewRepository.FindByTableReferenceAsync(tenantId, appId, tableKey, cancellationToken);
        blockers.AddRange(viewRefs.Select(view => new DeleteCheckBlockerDto("view", view.ViewKey, view.Name, $"/apps/{appId}/data/designer?viewKey={view.ViewKey}")));

        var canDelete = blockers.Count == 0;
        var warnings = canDelete
            ? new[] { "No blockers detected." }
            : Array.Empty<string>();
        return new DeleteCheckResultDto(canDelete, blockers, warnings);
    }

    public async Task<DeleteCheckResultDto> CheckViewDeleteAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var blockers = new List<DeleteCheckBlockerDto>();
        var referencingViews = await _viewRepository.FindReferencingViewAsync(tenantId, appId, viewKey, cancellationToken);
        blockers.AddRange(referencingViews
            .Where(view => !string.Equals(view.ViewKey, viewKey, StringComparison.OrdinalIgnoreCase))
            .Select(view => new DeleteCheckBlockerDto("view", view.ViewKey, view.Name, $"/apps/{appId}/data/designer?viewKey={view.ViewKey}")));

        var referencedPages = await _db.Queryable<LowCodePage>()
            .Where(page => page.TenantIdValue == tenantId.Value && page.DataTableKey == viewKey)
            .Select(page => new { page.Id, page.Name, page.AppId })
            .ToListAsync(cancellationToken);
        blockers.AddRange(referencedPages.Select(page => new DeleteCheckBlockerDto("page", page.Id.ToString(), page.Name, $"/apps/{page.AppId}/pages")));

        return new DeleteCheckResultDto(blockers.Count == 0, blockers, blockers.Count == 0 ? new[] { "No blockers detected." } : Array.Empty<string>());
    }
}

