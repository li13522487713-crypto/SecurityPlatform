using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Connectors;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class SoapWebServiceRuntimeConnector : IMicroflowRuntimeConnector
{
    private readonly ISoapWebServiceConnector _connector;

    public SoapWebServiceRuntimeConnector(ISoapWebServiceConnector connector)
    {
        _connector = connector;
    }

    public string Capability => MicroflowRuntimeConnectorCapability.SoapWebService;

    public bool Enabled => _connector.GetCapabilityStatus().Available;

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        => RuntimeConnectorAdapterGuard.ExecuteWithCapabilityGuardAsync(_connector.GetCapabilityStatus(), request, ct, _connector.ExecuteAsync);
}

public sealed class XmlImportMappingRuntimeConnector : IMicroflowRuntimeConnector
{
    private readonly IXmlMappingConnector _connector;

    public XmlImportMappingRuntimeConnector(IXmlMappingConnector connector)
    {
        _connector = connector;
    }

    public string Capability => MicroflowRuntimeConnectorCapability.XmlImportMapping;

    public bool Enabled => _connector.GetCapabilityStatus().Available;

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        => RuntimeConnectorAdapterGuard.ExecuteWithCapabilityGuardAsync(_connector.GetCapabilityStatus(), request, ct, _connector.ExecuteAsync);
}

public sealed class XmlExportMappingRuntimeConnector : IMicroflowRuntimeConnector
{
    private readonly IXmlMappingConnector _connector;

    public XmlExportMappingRuntimeConnector(IXmlMappingConnector connector)
    {
        _connector = connector;
    }

    public string Capability => MicroflowRuntimeConnectorCapability.XmlExportMapping;

    public bool Enabled => _connector.GetCapabilityStatus().Available;

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        => RuntimeConnectorAdapterGuard.ExecuteWithCapabilityGuardAsync(_connector.GetCapabilityStatus(), request, ct, _connector.ExecuteAsync);
}

public sealed class DocumentGenerationRuntimeConnector : IMicroflowRuntimeConnector
{
    private readonly IDocumentGenerationRuntime _connector;

    public DocumentGenerationRuntimeConnector(IDocumentGenerationRuntime connector)
    {
        _connector = connector;
    }

    public string Capability => MicroflowRuntimeConnectorCapability.DocumentGeneration;

    public bool Enabled => _connector.GetCapabilityStatus().Available;

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        => RuntimeConnectorAdapterGuard.ExecuteWithCapabilityGuardAsync(_connector.GetCapabilityStatus(), request, ct, _connector.ExecuteAsync);
}

public sealed class ExternalObjectRuntimeConnector : IMicroflowRuntimeConnector
{
    private readonly IExternalObjectConnector _connector;

    public ExternalObjectRuntimeConnector(IExternalObjectConnector connector)
    {
        _connector = connector;
    }

    public string Capability => MicroflowRuntimeConnectorCapability.ExternalObjectCrud;

    public bool Enabled => _connector.GetCapabilityStatus().Available;

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        => RuntimeConnectorAdapterGuard.ExecuteWithCapabilityGuardAsync(_connector.GetCapabilityStatus(), request, ct, _connector.ExecuteAsync);
}

internal static class RuntimeConnectorAdapterGuard
{
    public static Task<MicroflowConnectorExecutionResult> ExecuteWithCapabilityGuardAsync(
        MicroflowConnectorCapabilityStatus status,
        MicroflowConnectorExecutionRequest request,
        CancellationToken ct,
        Func<MicroflowConnectorExecutionRequest, CancellationToken, Task<MicroflowConnectorExecutionResult>> executor)
    {
        ct.ThrowIfCancellationRequested();
        if (!status.Available)
        {
            return Task.FromResult(new MicroflowConnectorExecutionResult
            {
                Success = false,
                Capability = request.Capability,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeConnectorRequired,
                    Message = string.IsNullOrWhiteSpace(status.Reason)
                        ? $"Connector capability '{request.Capability}' is not available."
                        : status.Reason,
                    ObjectId = request.ObjectId,
                    ActionId = request.ActionId
                }
            });
        }

        return executor(request, ct);
    }
}
