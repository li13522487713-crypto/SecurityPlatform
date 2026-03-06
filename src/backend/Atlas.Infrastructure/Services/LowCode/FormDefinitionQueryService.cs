using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class FormDefinitionQueryService : IFormDefinitionQueryService
{
    private readonly IFormDefinitionRepository _repository;
    private readonly IFormDefinitionVersionRepository _versionRepository;

    public FormDefinitionQueryService(
        IFormDefinitionRepository repository,
        IFormDefinitionVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<PagedResult<FormDefinitionListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, string? category = null,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _repository.GetPagedAsync(
            tenantId, request.PageIndex, request.PageSize, request.Keyword, category, cancellationToken);

        var mapped = items.Select(e => new FormDefinitionListItem(
            e.Id.ToString(),
            e.Name,
            e.Description,
            e.Category,
            e.Version,
            e.Status.ToString(),
            e.CreatedAt,
            e.UpdatedAt,
            e.CreatedBy,
            e.DataTableKey,
            e.Icon,
            e.PublishedAt,
            e.DeprecatedAt
        )).ToList();

        return new PagedResult<FormDefinitionListItem>(mapped, total, request.PageIndex, request.PageSize);
    }

    public async Task<FormDefinitionDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new FormDefinitionDetail(
            entity.Id.ToString(),
            entity.Name,
            entity.Description,
            entity.Category,
            entity.SchemaJson,
            entity.Version,
            entity.Status.ToString(),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            entity.UpdatedBy,
            entity.DataTableKey,
            entity.Icon,
            entity.PublishedAt,
            entity.PublishedBy,
            entity.DeprecatedAt
        );
    }

    public async Task<IReadOnlyList<FormDefinitionVersionListItem>> GetVersionsAsync(
        TenantId tenantId, long formDefinitionId, CancellationToken cancellationToken = default)
    {
        var versions = await _versionRepository.GetByFormDefinitionIdAsync(tenantId, formDefinitionId, cancellationToken);

        return versions.Select(v => new FormDefinitionVersionListItem(
            v.Id.ToString(),
            v.FormDefinitionId.ToString(),
            v.SnapshotVersion,
            v.Name,
            v.Description,
            v.Category,
            v.DataTableKey,
            v.Icon,
            v.CreatedBy,
            v.CreatedAt
        )).ToList();
    }

    public async Task<FormDefinitionVersionDetail?> GetVersionByIdAsync(
        TenantId tenantId, long versionId, CancellationToken cancellationToken = default)
    {
        var v = await _versionRepository.GetByIdAsync(tenantId, versionId, cancellationToken);
        if (v is null)
        {
            return null;
        }

        return new FormDefinitionVersionDetail(
            v.Id.ToString(),
            v.FormDefinitionId.ToString(),
            v.SnapshotVersion,
            v.Name,
            v.Description,
            v.Category,
            v.SchemaJson,
            v.DataTableKey,
            v.Icon,
            v.CreatedBy,
            v.CreatedAt
        );
    }
}
