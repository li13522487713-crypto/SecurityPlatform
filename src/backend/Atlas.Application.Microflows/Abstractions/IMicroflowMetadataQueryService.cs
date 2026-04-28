using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowMetadataQueryService
{
    Task<MicroflowMetadataCatalogDto> GetCatalogAsync(MicroflowMetadataQueryDto query, CancellationToken cancellationToken);
}

public sealed record MicroflowMetadataQueryDto
{
    public string? WorkspaceId { get; init; }

    public string? ModuleId { get; init; }

    public bool IncludeSystem { get; init; }

    public bool IncludeArchived { get; init; }
}
