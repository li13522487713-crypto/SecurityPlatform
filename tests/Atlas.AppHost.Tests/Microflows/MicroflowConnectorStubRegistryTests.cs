using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Runtime.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowConnectorStubRegistryTests
{
    [Theory]
    [InlineData(typeof(ISoapWebServiceConnector), "soap.webService")]
    [InlineData(typeof(IXmlMappingConnector), "xml.mapping")]
    [InlineData(typeof(IDocumentGenerationRuntime), "document.generation")]
    [InlineData(typeof(IWorkflowRuntimeClient), "workflow.action")]
    [InlineData(typeof(IMlRuntime), "ml.model")]
    [InlineData(typeof(IExternalActionConnector), "external.action")]
    [InlineData(typeof(IExternalObjectConnector), "externalObject.crud")]
    [InlineData(typeof(IServerActionRuntime), "java.action")]
    public void ConnectorStubResolvesAsUnavailableCapability(Type serviceType, string capability)
    {
        using var provider = new ServiceCollection()
            .AddAtlasApplicationMicroflows()
            .BuildServiceProvider();

        var service = provider.GetRequiredService(serviceType);
        var probe = Assert.IsAssignableFrom<IMicroflowRuntimeCapabilityProbe>(service);
        var status = probe.GetCapabilityStatus();

        Assert.Equal(capability, status.Capability);
        Assert.False(status.Available);
        Assert.Equal("RUNTIME_CONNECTOR_REQUIRED", status.Reason);
    }
}
