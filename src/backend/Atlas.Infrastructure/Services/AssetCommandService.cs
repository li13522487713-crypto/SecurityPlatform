using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Repositories;
using Atlas.Domain.Assets.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class AssetCommandService : IAssetCommandService
{
    private readonly IAssetRepository _repository;

    public AssetCommandService(IAssetRepository repository)
    {
        _repository = repository;
    }

    public Task<long> CreateAsync(Asset asset, CancellationToken cancellationToken)
    {
        return _repository.AddAsync(asset, cancellationToken);
    }
}