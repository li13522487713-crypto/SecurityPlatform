using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowMetadataService
{
    Task<MicroflowMetadataCatalogDto> GetCatalogAsync(GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken);

    Task<MetadataEntityDto> GetEntityAsync(string qualifiedName, GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken);

    Task<MetadataEnumerationDto> GetEnumerationAsync(string qualifiedName, GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataMicroflowRefDto>> GetMicroflowRefsAsync(GetMicroflowRefsRequestDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataPageRefDto>> GetPageRefsAsync(GetPageRefsRequestDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataWorkflowRefDto>> GetWorkflowRefsAsync(GetWorkflowRefsRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowMetadataHealthDto> GetHealthAsync(string? workspaceId, CancellationToken cancellationToken);
}
