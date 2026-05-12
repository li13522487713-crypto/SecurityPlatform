using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class ConnectorBackedActionExecutorsTests
{
    [Fact]
    public async Task XmlExecutor_WithCapabilityAndInvalidPayload_ReturnsValidationBlocked()
    {
        var registry = new MicroflowActionExecutorRegistry(BuildServiceProvider());
        var executor = registry.GetOrFallback("importXml");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = MicroflowRuntimeConnectorCapability.XmlImportMapping,
                OutputJson = "{\"ok\":true}"
            });

        var result = await executor.ExecuteAsync(
            Context("importXml", new { }, connectorRegistry),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeValidationBlocked, result.Error?.Code);
        Assert.Empty(connectorRegistry.Requests);
    }

    [Fact]
    public async Task XmlExecutor_WithCapability_DelegatesToConnectorRegistry()
    {
        var registry = new MicroflowActionExecutorRegistry(BuildServiceProvider());
        var executor = registry.GetOrFallback("exportXml");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = MicroflowRuntimeConnectorCapability.XmlExportMapping,
                OutputJson = "{\"ok\":true,\"provider\":\"xml\"}"
            });

        var result = await executor.ExecuteAsync(
            Context("exportXml", new { xml = "<a/>" }, connectorRegistry),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Single(connectorRegistry.Requests);
        Assert.Equal(MicroflowRuntimeConnectorCapability.XmlExportMapping, connectorRegistry.Requests[0].Capability);
        Assert.True(result.OutputJson?.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ExternalObjectExecutor_WithCapability_DelegatesToConnectorRegistry()
    {
        var registry = new MicroflowActionExecutorRegistry(BuildServiceProvider());
        var executor = registry.GetOrFallback("createExternalObject");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = MicroflowRuntimeConnectorCapability.ExternalObjectCrud,
                OutputJson = "{\"objectId\":\"ext-1\"}"
            });

        var result = await executor.ExecuteAsync(
            Context("createExternalObject", new { externalObjectType = "crm.customer", payload = new { id = "c1" } }, connectorRegistry),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Single(connectorRegistry.Requests);
        Assert.Equal(MicroflowRuntimeConnectorCapability.ExternalObjectCrud, connectorRegistry.Requests[0].Capability);
        Assert.Equal("ext-1", result.OutputJson?.GetProperty("objectId").GetString());
    }

    [Fact]
    public async Task DocumentExecutor_WithoutCapability_ReturnsConnectorRequired()
    {
        var registry = new MicroflowActionExecutorRegistry(BuildServiceProvider());
        var executor = registry.GetOrFallback("generateDocument");

        var result = await executor.ExecuteAsync(
            Context("generateDocument", new { templateId = "doc-1" }),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.ConnectorRequired, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeConnectorRequired, result.Error?.Code);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.ConnectorCapability == MicroflowRuntimeConnectorCapability.DocumentGeneration);
    }

    [Fact]
    public async Task SoapExecutor_WithConnectorFault_PropagatesLatestSoapFault()
    {
        var registry = new MicroflowActionExecutorRegistry(BuildServiceProvider());
        var executor = registry.GetOrFallback("webServiceCall");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = false,
                Capability = MicroflowRuntimeConnectorCapability.SoapWebService,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = "SOAP_REMOTE_FAULT",
                    Message = "Remote SOAP fault.",
                    Details = "fault detail"
                },
                LatestSoapFault = JsonSerializer.SerializeToElement(new
                {
                    faultCode = "Server.Validation",
                    faultString = "Order invalid",
                    detail = "fault detail"
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });

        var result = await executor.ExecuteAsync(
            Context("webServiceCall", new { endpoint = "https://soap.test/service", operation = "SubmitOrder" }, connectorRegistry),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.True(result.LatestSoapFault.HasValue);
        Assert.Equal("Server.Validation", result.LatestSoapFault.Value.GetProperty("faultCode").GetString());
        Assert.Equal("Order invalid", result.LatestSoapFault.Value.GetProperty("faultString").GetString());
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<SoapWebServiceActionExecutor>();
        services.AddScoped<XmlMappingActionExecutor>();
        services.AddScoped<DocumentGenerationActionExecutor>();
        services.AddScoped<ExternalObjectActionExecutor>();
        return services.BuildServiceProvider();
    }

    private static MicroflowActionExecutionContext Context(
        string actionKind,
        object? config = null,
        IMicroflowRuntimeConnectorRegistry? connectorRegistry = null)
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-connector-executor-test",
            SchemaId = "schema-connector-executor-test",
            StartNodeId = "start"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-connector-executor-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            input: null,
            securityContext: null,
            startedAt: DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "action", ActionId = "action-id", ActionKind = actionKind },
            ActionKind = actionKind,
            ObjectId = "action",
            ActionId = "action-id",
            ActionConfig = JsonSerializer.SerializeToElement(config ?? new { }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            VariableStore = runtime.VariableStore,
            ExpressionEvaluator = new MicroflowExpressionEvaluator(),
            ConnectorRegistry = connectorRegistry ?? new MicroflowRuntimeConnectorRegistry()
        };
    }

    private sealed class StubConnectorRegistry : IMicroflowRuntimeConnectorRegistry
    {
        private readonly bool _hasCapability;
        private readonly MicroflowConnectorExecutionResult _result;

        public StubConnectorRegistry(bool hasCapability, MicroflowConnectorExecutionResult result)
        {
            _hasCapability = hasCapability;
            _result = result;
        }

        public List<MicroflowConnectorExecutionRequest> Requests { get; } = [];

        public bool HasCapability(string capability) => _hasCapability;

        public IReadOnlyList<string> ListEnabledCapabilities()
            => _hasCapability ? [_result.Capability] : Array.Empty<string>();

        public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }
}
