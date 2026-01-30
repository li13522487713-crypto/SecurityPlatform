using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class PositionCommandService : IPositionCommandService
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUserPositionRepository _userPositionRepository;

    public PositionCommandService(
        IPositionRepository positionRepository,
        IUserPositionRepository userPositionRepository)
    {
        _positionRepository = positionRepository;
        _userPositionRepository = userPositionRepository;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        PositionCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _positionRepository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException("Position code already exists.", ErrorCodes.ValidationError);
        }

        var position = new Position(tenantId, request.Name, request.Code, id);
        position.Update(request.Name, request.Description, request.IsActive, request.SortOrder);
        await _positionRepository.AddAsync(position, cancellationToken);
        return position.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long positionId,
        PositionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var position = await _positionRepository.FindByIdAsync(tenantId, positionId, cancellationToken);
        if (position is null)
        {
            throw new BusinessException("Position not found.", ErrorCodes.NotFound);
        }

        position.Update(request.Name, request.Description, request.IsActive, request.SortOrder);
        await _positionRepository.UpdateAsync(position, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long positionId,
        CancellationToken cancellationToken)
    {
        var position = await _positionRepository.FindByIdAsync(tenantId, positionId, cancellationToken);
        if (position is null)
        {
            throw new BusinessException("Position not found.", ErrorCodes.NotFound);
        }

        if (position.IsSystem)
        {
            throw new BusinessException("System position cannot be deleted.", ErrorCodes.Forbidden);
        }

        var hasUsers = await _userPositionRepository.ExistsByPositionIdAsync(tenantId, positionId, cancellationToken);
        if (hasUsers)
        {
            throw new BusinessException("Position is assigned to users.", ErrorCodes.ValidationError);
        }

        await _positionRepository.DeleteAsync(tenantId, positionId, cancellationToken);
    }
}
