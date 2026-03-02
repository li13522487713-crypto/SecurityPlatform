using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class FormDefinitionQueryService : IFormDefinitionQueryService
{
    private readonly IFormDefinitionRepository _repository;

    public FormDefinitionQueryService(IFormDefinitionRepository repository)
    {
        _repository = repository;
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
            e.PublishedAt
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
            entity.PublishedBy
        );
    }
}
