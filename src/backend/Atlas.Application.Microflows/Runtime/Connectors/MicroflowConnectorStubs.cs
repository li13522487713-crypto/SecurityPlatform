using System.Text.Json;

namespace Atlas.Application.Microflows.Runtime.Connectors;

public sealed record MicroflowConnectorCapabilityStatus(
    string Capability,
    bool Available,
    string Reason = "RUNTIME_CONNECTOR_REQUIRED");

public interface IMicroflowRuntimeCapabilityProbe
{
    MicroflowConnectorCapabilityStatus GetCapabilityStatus();
}

public interface ISoapWebServiceConnector : IMicroflowRuntimeCapabilityProbe;

public interface IXmlMappingConnector : IMicroflowRuntimeCapabilityProbe;

public interface IDocumentGenerationRuntime : IMicroflowRuntimeCapabilityProbe;

public interface IWorkflowRuntimeClient : IMicroflowRuntimeCapabilityProbe
{
    Task<WorkflowRuntimeStartResult> StartWorkflowAsync(
        string workflowId,
        int? version,
        JsonElement? data,
        string? reference,
        CancellationToken cancellationToken);

    Task<WorkflowRuntimeCommandResult> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken);

    Task<WorkflowRuntimeCommandResult> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken);

    Task<WorkflowRuntimeCommandResult> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken);

    Task<WorkflowRuntimeCommandResult> PublishEventAsync(
        string eventName,
        string eventKey,
        JsonElement? eventData,
        CancellationToken cancellationToken);
}

public sealed record WorkflowRuntimeStartResult
{
    public bool Success { get; init; }
    public string? InstanceId { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record WorkflowRuntimeCommandResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IMlRuntime : IMicroflowRuntimeCapabilityProbe;

public interface IExternalActionConnector : IMicroflowRuntimeCapabilityProbe;

public interface IExternalObjectConnector : IMicroflowRuntimeCapabilityProbe;

public interface IServerActionRuntime : IMicroflowRuntimeCapabilityProbe;

public abstract class MissingMicroflowConnectorCapability : IMicroflowRuntimeCapabilityProbe
{
    protected MissingMicroflowConnectorCapability(string capability)
    {
        Capability = capability;
    }

    public string Capability { get; }

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new(Capability, Available: false);
}

public sealed class MissingSoapWebServiceConnector : MissingMicroflowConnectorCapability, ISoapWebServiceConnector
{
    public MissingSoapWebServiceConnector() : base("soap.webService") { }
}

public sealed class MissingXmlMappingConnector : MissingMicroflowConnectorCapability, IXmlMappingConnector
{
    public MissingXmlMappingConnector() : base("xml.mapping") { }
}

public sealed class MissingDocumentGenerationRuntime : MissingMicroflowConnectorCapability, IDocumentGenerationRuntime
{
    public MissingDocumentGenerationRuntime() : base("document.generation") { }
}

public sealed class MissingWorkflowRuntimeClient : MissingMicroflowConnectorCapability, IWorkflowRuntimeClient
{
    public MissingWorkflowRuntimeClient() : base("workflow.action") { }

    public Task<WorkflowRuntimeStartResult> StartWorkflowAsync(
        string workflowId,
        int? version,
        JsonElement? data,
        string? reference,
        CancellationToken cancellationToken)
        => Task.FromResult(new WorkflowRuntimeStartResult
        {
            Success = false,
            ErrorMessage = "Workflow runtime connector required."
        });

    public Task<WorkflowRuntimeCommandResult> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => Task.FromResult(Failed());

    public Task<WorkflowRuntimeCommandResult> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => Task.FromResult(Failed());

    public Task<WorkflowRuntimeCommandResult> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => Task.FromResult(Failed());

    public Task<WorkflowRuntimeCommandResult> PublishEventAsync(
        string eventName,
        string eventKey,
        JsonElement? eventData,
        CancellationToken cancellationToken)
        => Task.FromResult(Failed());

    private static WorkflowRuntimeCommandResult Failed()
        => new()
        {
            Success = false,
            ErrorMessage = "Workflow runtime connector required."
        };
}

public sealed class MissingMlRuntime : MissingMicroflowConnectorCapability, IMlRuntime
{
    public MissingMlRuntime() : base("ml.model") { }
}

public sealed class MissingExternalActionConnector : MissingMicroflowConnectorCapability, IExternalActionConnector
{
    public MissingExternalActionConnector() : base("external.action") { }
}

public sealed class MissingExternalObjectConnector : MissingMicroflowConnectorCapability, IExternalObjectConnector
{
    public MissingExternalObjectConnector() : base("externalObject.crud") { }
}

public sealed class MissingServerActionRuntime : MissingMicroflowConnectorCapability, IServerActionRuntime
{
    public MissingServerActionRuntime() : base("java.action") { }
}
