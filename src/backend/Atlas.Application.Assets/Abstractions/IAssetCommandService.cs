using Atlas.Domain.Assets.Entities;

namespace Atlas.Application.Assets.Abstractions;

public interface IAssetCommandService
{
    Task<long> CreateAsync(Asset asset, CancellationToken cancellationToken);
}