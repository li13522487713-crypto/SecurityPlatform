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
    private readonly IProjectPositionRepository _projectPositionRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;

    public PositionCommandService(
        IPositionRepository positionRepository,
        IUserPositionRepository userPositionRepository,
        IProjectPositionRepository projectPositionRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor)
    {
        _positionRepository = positionRepository;
        _userPositionRepository = userPositionRepository;
        _projectPositionRepository = projectPositionRepository;
        _projectContextAccessor = projectContextAccessor;
        _idGeneratorAccessor = idGeneratorAccessor;
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

        var projectContext = _projectContextAccessor.GetCurrent();
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            await _projectPositionRepository.AddRangeAsync(
                new[] { new ProjectPosition(tenantId, projectContext.ProjectId.Value, position.Id, _idGeneratorAccessor.NextId()) },
                cancellationToken);
        }
        return position.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long positionId,
        PositionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsurePositionInProjectAsync(tenantId, positionId, cancellationToken);
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
        await EnsurePositionInProjectAsync(tenantId, positionId, cancellationToken);
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

        await _projectPositionRepository.DeleteByPositionIdAsync(tenantId, positionId, cancellationToken);
        await _positionRepository.DeleteAsync(tenantId, positionId, cancellationToken);
    }

    private async Task EnsurePositionInProjectAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        if (!projectContext.IsEnabled || !projectContext.ProjectId.HasValue)
        {
            return;
        }

        var relations = await _projectPositionRepository.QueryByProjectIdAsync(
            tenantId,
            projectContext.ProjectId.Value,
            cancellationToken);
        if (!relations.Any(x => x.PositionId == positionId))
        {
            throw new BusinessException("Position not in current project.", ErrorCodes.Forbidden);
        }
    }
}
