using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Calls;

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

    Task<MicroflowRunSessionDto> GetRunSessionAsync(
        string resourceId,
        string runId,
        CancellationToken cancellationToken);

    Task<GetMicroflowRunTraceResponse> GetRunTraceAsync(
        string runId,
        CancellationToken cancellationToken);

    Task<ListMicroflowRunsResponse> ListRunsAsync(
        string resourceId,
        ListMicroflowRunsRequest request,
        CancellationToken cancellationToken);
}

public sealed record MicroflowExecutionRequest
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

    public string? CorrelationId { get; init; }

    public RuntimeExecutionContext? ParentRuntimeContext { get; init; }

    public MicroflowCallStackFrame? CallFrame { get; init; }

    public string TransactionBoundary { get; init; } = MicroflowCallTransactionBoundary.Inherit;

    public int MaxCallDepth { get; init; } = 10;
}
