using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Connectors;
using Atlas.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class RuntimeConnectorAdaptersTests
{
    [Fact]
    public async Task SoapRuntimeConnector_WhenCapabilityUnavailable_ReturnsConnectorRequired()
    {
        var connector = new StubSoapConnector(
            new MicroflowConnectorCapabilityStatus(MicroflowRuntimeConnectorCapability.SoapWebService, Available: false, "soap connector unavailable"),
            new MicroflowConnectorExecutionResult { Success = true, Capability = MicroflowRuntimeConnectorCapability.SoapWebService });
        var adapter = new SoapWebServiceRuntimeConnector(connector);

        var result = await adapter.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.SoapWebService,
                ActionKind = "webServiceCall",
                ObjectId = "node-1",
                ActionId = "action-1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(RuntimeErrorCode.RuntimeConnectorRequired, result.Error?.Code);
        Assert.Equal(0, connector.ExecuteCount);
    }

    [Fact]
    public async Task XmlImportRuntimeConnector_WhenAvailable_DelegatesToConnector()
    {
        var connector = new StubXmlConnector(
            new MicroflowConnectorCapabilityStatus("xml.mapping", Available: true, "available"),
            new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = MicroflowRuntimeConnectorCapability.XmlImportMapping,
                OutputJson = "{\"ok\":true}"
            });
        var adapter = new XmlImportMappingRuntimeConnector(connector);

        var result = await adapter.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.XmlImportMapping,
                ActionKind = "importXml"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, connector.ExecuteCount);
        Assert.Equal(MicroflowRuntimeConnectorCapability.XmlImportMapping, result.Capability);
    }

    [Fact]
    public void AddAtlasApplicationMicroflows_RegistersConnectorAdapters()
    {
        var services = new ServiceCollection();
        services.AddAtlasApplicationMicroflows();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var connectors = scope.ServiceProvider.GetServices<IMicroflowRuntimeConnector>().ToArray();

        Assert.Contains(connectors, connector => connector is SoapWebServiceRuntimeConnector);
        Assert.Contains(connectors, connector => connector is XmlImportMappingRuntimeConnector);
        Assert.Contains(connectors, connector => connector is XmlExportMappingRuntimeConnector);
        Assert.Contains(connectors, connector => connector is DocumentGenerationRuntimeConnector);
        Assert.Contains(connectors, connector => connector is ExternalObjectRuntimeConnector);
    }

    [Fact]
    public void ApplicationAndInfrastructureRegistration_EnablesDefaultConnectorCapabilities()
    {
        var services = new ServiceCollection();
        services.AddAtlasApplicationMicroflows();
        services.AddMicroflowInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IMicroflowRuntimeConnectorRegistry>();

        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.SoapWebService));
        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.XmlImportMapping));
        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.XmlExportMapping));
        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.DocumentGeneration));
        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.ExternalObjectCrud));
    }

    private sealed class StubSoapConnector : ISoapWebServiceConnector
    {
        private readonly MicroflowConnectorCapabilityStatus _status;
        private readonly MicroflowConnectorExecutionResult _result;

        public StubSoapConnector(
            MicroflowConnectorCapabilityStatus status,
            MicroflowConnectorExecutionResult result)
        {
            _status = status;
            _result = result;
        }

        public int ExecuteCount { get; private set; }

        public MicroflowConnectorCapabilityStatus GetCapabilityStatus() => _status;

        public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken cancellationToken)
        {
            ExecuteCount += 1;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubXmlConnector : IXmlMappingConnector
    {
        private readonly MicroflowConnectorCapabilityStatus _status;
        private readonly MicroflowConnectorExecutionResult _result;

        public StubXmlConnector(
            MicroflowConnectorCapabilityStatus status,
            MicroflowConnectorExecutionResult result)
        {
            _status = status;
            _result = result;
        }

        public int ExecuteCount { get; private set; }

        public MicroflowConnectorCapabilityStatus GetCapabilityStatus() => _status;

        public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken cancellationToken)
        {
            ExecuteCount += 1;
            return Task.FromResult(_result);
        }
    }
}
