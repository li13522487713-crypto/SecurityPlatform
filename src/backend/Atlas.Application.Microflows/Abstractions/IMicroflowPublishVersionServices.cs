using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowPublishService
{
    Task<MicroflowPublishResultDto> PublishAsync(string resourceId, PublishMicroflowApiRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowPublishImpactAnalysisDto> AnalyzeImpactAsync(string resourceId, AnalyzeMicroflowImpactRequestDto request, CancellationToken cancellationToken);
}

public interface IMicroflowVersionService
{
    Task<IReadOnlyList<MicroflowVersionSummaryDto>> ListVersionsAsync(string resourceId, CancellationToken cancellationToken);

    Task<MicroflowVersionDetailDto> GetVersionDetailAsync(string resourceId, string versionId, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> RollbackAsync(string resourceId, string versionId, RollbackMicroflowVersionRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> DuplicateVersionAsync(string resourceId, string versionId, DuplicateMicroflowVersionRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowVersionDiffDto> CompareCurrentAsync(string resourceId, string versionId, CancellationToken cancellationToken);
}

public interface IMicroflowVersionDiffService
{
    MicroflowVersionDiffDto Compare(JsonElement beforeSchema, JsonElement afterSchema);
}

public interface IMicroflowPublishImpactService
{
    MicroflowPublishImpactAnalysisDto Analyze(
        MicroflowResourceEntity resource,
        JsonElement currentSchema,
        JsonElement? latestPublishedSchema,
        IReadOnlyList<MicroflowReferenceEntity> references,
        string nextVersion);
}
