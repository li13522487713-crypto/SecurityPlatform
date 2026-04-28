using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowReferenceIndexer
{
    Task<IReadOnlyList<MicroflowReferenceEntity>> RebuildReferencesForMicroflowAsync(string resourceId, CancellationToken cancellationToken);

    IReadOnlyList<MicroflowReferenceEntity> ExtractReferencesFromSchema(MicroflowResourceEntity sourceResource, JsonElement schema);
}

public interface IMicroflowReferenceService
{
    Task<IReadOnlyList<MicroflowReferenceDto>> GetReferencesAsync(
        string targetMicroflowId,
        GetMicroflowReferencesRequestDto request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowReferenceDto>> RebuildReferencesAsync(string resourceId, CancellationToken cancellationToken);

    Task<int> RebuildAllReferencesAsync(string? workspaceId, CancellationToken cancellationToken);
}
