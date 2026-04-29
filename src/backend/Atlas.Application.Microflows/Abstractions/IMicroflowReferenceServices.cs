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

    /// <summary>列出哪些 source 调用了该微流（reference table 中 targetMicroflowId == resourceId）。</summary>
    Task<IReadOnlyList<MicroflowReferenceDto>> ListCallersAsync(
        string resourceId,
        GetMicroflowReferencesRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>列出该微流调用了哪些下游 microflow（reference table 中 sourceType=microflow && sourceId=resourceId）。</summary>
    Task<IReadOnlyList<MicroflowReferenceDto>> ListCalleesAsync(
        string resourceId,
        GetMicroflowReferencesRequestDto request,
        CancellationToken cancellationToken);
}
