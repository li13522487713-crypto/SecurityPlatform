using System.Text.Json;
using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Connectors;
using Atlas.Infrastructure.DependencyInjection;
using Atlas.Infrastructure.Services.Microflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class DefaultMicroflowConnectorsTests
{
    [Fact]
    public async Task DefaultXmlConnector_ImportXml_ReturnsRootSummary()
    {
        var connector = new DefaultXmlMappingConnector();
        var result = await connector.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.XmlImportMapping,
                ActionKind = "importXml",
                PayloadJson = "{\"xml\":\"<order id='1'><line /></order>\"}"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MicroflowRuntimeConnectorCapability.XmlImportMapping, result.Capability);
        using var output = JsonDocument.Parse(result.OutputJson!);
        Assert.Equal("order", output.RootElement.GetProperty("root").GetString());
    }

    [Fact]
    public async Task DefaultDocumentConnector_GenerateDocument_ReturnsDocumentId()
    {
        var connector = new DefaultDocumentGenerationRuntime();
        var result = await connector.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.DocumentGeneration,
                ActionKind = "generateDocument",
                PayloadJson = "{\"templateId\":\"invoice-template\"}"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        using var output = JsonDocument.Parse(result.OutputJson!);
        Assert.Equal("invoice-template", output.RootElement.GetProperty("templateId").GetString());
        Assert.StartsWith("doc-", output.RootElement.GetProperty("documentId").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefaultExternalObjectConnector_CreateAndChange_WorksInMemory()
    {
        var connector = new DefaultExternalObjectConnector();

        var create = await connector.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.ExternalObjectCrud,
                ActionKind = "createExternalObject",
                PayloadJson = "{\"objectId\":\"ext-100\",\"externalObjectType\":\"crm.customer\"}"
            },
            CancellationToken.None);
        Assert.True(create.Success);

        var update = await connector.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.ExternalObjectCrud,
                ActionKind = "changeExternalObject",
                PayloadJson = "{\"objectId\":\"ext-100\",\"externalObjectType\":\"crm.customer\"}"
            },
            CancellationToken.None);
        Assert.True(update.Success);
        using var output = JsonDocument.Parse(update.OutputJson!);
        Assert.Equal("updated", output.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void MicroflowInfrastructure_OverridesMissingConnectorRegistrations()
    {
        var services = new ServiceCollection();
        services.AddAtlasApplicationMicroflows();
        services.AddMicroflowInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DefaultSoapWebServiceConnector>(scope.ServiceProvider.GetRequiredService<ISoapWebServiceConnector>());
        Assert.IsType<DefaultXmlMappingConnector>(scope.ServiceProvider.GetRequiredService<IXmlMappingConnector>());
        Assert.IsType<DefaultDocumentGenerationRuntime>(scope.ServiceProvider.GetRequiredService<IDocumentGenerationRuntime>());
        Assert.IsType<DefaultExternalObjectConnector>(scope.ServiceProvider.GetRequiredService<IExternalObjectConnector>());
    }
}

