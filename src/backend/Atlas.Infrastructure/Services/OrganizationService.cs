using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class OrganizationService : IOrganizationService
{
    private readonly OrganizationRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public OrganizationService(OrganizationRepository repository, IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListByTenantAsync(tenantId, cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<OrganizationDto?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        var entity = await _repository.FindByCodeAsync(tenantId, code, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<OrganizationDto> CreateAsync(TenantId tenantId, long createdBy, OrganizationCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessException(ErrorCodes.ValidationError, "OrganizationCodeRequired");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessException(ErrorCodes.ValidationError, "OrganizationNameRequired");

        var existing = await _repository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.Conflict, "OrganizationCodeDuplicate");
        }
        var entity = new Organization(tenantId, request.Code, request.Name, request.Description, isDefault: false, createdBy, _idGenerator.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task UpdateAsync(TenantId tenantId, long id, long updatedBy, OrganizationUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "OrganizationNotFound");
        entity.Update(request.Name, request.Description, updatedBy);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null) return;
        if (entity.IsDefault)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DefaultOrganizationNotDeletable");
        }
        await _repository.DeleteAsync(entity, cancellationToken);
    }

    public async Task<OrganizationDto> GetOrCreateDefaultAsync(TenantId tenantId, long createdBy, CancellationToken cancellationToken)
    {
        var existing = await _repository.FindDefaultAsync(tenantId, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }
        var entity = new Organization(tenantId, code: "default", name: "Default Org", description: null, isDefault: true, createdBy, _idGenerator.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    internal static OrganizationDto ToDto(Organization entity)
    {
        return new OrganizationDto(
            Id: entity.Id.ToString(),
            Code: entity.Code,
            Name: entity.Name,
            Description: string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
            IsDefault: entity.IsDefault,
            CreatedBy: entity.CreatedBy,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
