using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowTestRunService
{
    Task<TestRunMicroflowApiResponse> TestRunAsync(
        string resourceId,
        TestRunMicroflowApiRequest request,
        CancellationToken cancellationToken);

    Task<CancelMicroflowRunResponse> CancelAsync(
        string runId,
        CancellationToken cancellationToken);

    Task<MicroflowRunSessionDto> GetRunSessionAsync(
        string runId,
        CancellationToken cancellationToken);

    Task<GetMicroflowRunTraceResponse> GetRunTraceAsync(
        string runId,
        CancellationToken cancellationToken);
}

public interface IMicroflowMockRuntimeRunner
{
    Task<MicroflowRunSessionDto> RunAsync(
        MicroflowMockRuntimeRequest request,
        CancellationToken cancellationToken);
}

public sealed record MicroflowMockRuntimeRequest
{
    public string ResourceId { get; init; } = string.Empty;

    public string SchemaId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public JsonElement Schema { get; init; }

    public MicroflowExecutionPlan? ExecutionPlan { get; init; }

    public IReadOnlyDictionary<string, JsonElement> Input { get; init; } = new Dictionary<string, JsonElement>();

    public MicroflowTestRunOptionsDto Options { get; init; } = new();

    public MicroflowMetadataCatalogDto? Metadata { get; init; }

    public MicroflowRequestContext RequestContext { get; init; } = new();
}
