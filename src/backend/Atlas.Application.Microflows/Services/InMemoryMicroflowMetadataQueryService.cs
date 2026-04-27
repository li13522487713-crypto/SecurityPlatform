using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class InMemoryMicroflowMetadataQueryService : IMicroflowMetadataQueryService
{
    private readonly IMicroflowClock _clock;

    public InMemoryMicroflowMetadataQueryService(IMicroflowClock clock)
    {
        _clock = clock;
    }

    public Task<MicroflowMetadataCatalogDto> GetCatalogAsync(MicroflowMetadataQueryDto query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new MicroflowMetadataCatalogDto
        {
            Version = "backend-skeleton",
            UpdatedAt = _clock.UtcNow
        });
    }
}
