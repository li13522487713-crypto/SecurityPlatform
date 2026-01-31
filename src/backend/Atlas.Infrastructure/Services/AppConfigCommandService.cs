using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AppConfigCommandService : IAppConfigCommandService
{
    private readonly IAppConfigRepository _repository;

    public AppConfigCommandService(IAppConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        AppConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var appConfig = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        if (appConfig is null)
        {
            throw new BusinessException("App config not found.", ErrorCodes.NotFound);
        }

        appConfig.Update(
            request.Name,
            request.IsActive,
            request.EnableProjectScope,
            request.Description,
            request.SortOrder);

        await _repository.UpdateAsync(appConfig, cancellationToken);
    }
}
