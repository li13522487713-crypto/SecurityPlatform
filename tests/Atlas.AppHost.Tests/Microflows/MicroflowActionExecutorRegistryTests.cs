using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using System.Text.Json;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowActionExecutorRegistryTests
{
    private static readonly string[] FrontendActionKinds =
    [
        "retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "cast",
        "aggregateList", "createList", "changeList", "listOperation", "filterList", "sortList",
        "createVariable", "declareLocalVariable", "changeVariable",
        "callMicroflow", "callJavaAction", "callJavaScriptAction", "callNanoflow",
        "restCall", "webServiceCall", "importXml", "exportXml", "callExternalAction", "restOperationCall", "queryExternalDatabase",
        "closePage", "downloadFile", "showHomePage", "showMessage", "showPage", "validationFeedback", "synchronize",
        "logMessage", "throwException",
        "generateDocument",
        "counter", "incrementCounter", "gauge",
        "mlModelCall",
        "applyJumpToOption", "callWorkflow", "changeWorkflowState", "completeUserTask", "generateJumpToOptions",
        "retrieveWorkflowActivityRecords", "retrieveWorkflowContext", "retrieveWorkflows", "showUserTaskPage",
        "showWorkflowAdminPage", "lockWorkflow", "unlockWorkflow", "notifyWorkflow",
        "deleteExternalObject", "sendExternalObject",
        "sendEmail", "sendNotification", "publishMessage", "consumeMessage",
        "callODataAction", "retrieveODataObject", "commitODataObject", "deleteODataObject",
        "retrieveFileDocument", "storeFileDocument", "exportFileDocument", "importFileDocument",
        "createExternalObject", "changeExternalObject"
    ];

    [Fact]
    public void Registry_CoversEveryFrontendActionKind()
    {
        var registry = new MicroflowActionExecutorRegistry();

        var coverage = registry.ValidateCoverage(FrontendActionKinds);

        Assert.True(coverage.Covered, string.Join(", ", coverage.MissingActionKinds));
        Assert.Equal(FrontendActionKinds.Length, coverage.ExpectedCount);
        Assert.All(FrontendActionKinds, kind => Assert.True(registry.TryGet(kind, out _), $"Missing executor for {kind}"));
    }

    [Fact]
    public async Task RuntimeCommandExecutor_ProducesClientCommand()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback("showMessage");

        var result = await executor.ExecuteAsync(Context("showMessage"), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.PendingClientCommand, result.Status);
        Assert.Single(result.RuntimeCommands);
        Assert.Equal("showMessage", result.RuntimeCommands[0].CommandKind);
    }

    [Theory]
    [InlineData("showPage")]
    [InlineData("showHomePage")]
    [InlineData("showMessage")]
    [InlineData("closePage")]
    [InlineData("validationFeedback")]
    [InlineData("downloadFile")]
    [InlineData("callJavaScriptAction")]
    [InlineData("callNanoflow")]
    [InlineData("synchronize")]
    public async Task RuntimeCommandFamily_ProducesClientCommand(string actionKind)
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback(actionKind);

        var result = await executor.ExecuteAsync(Context(actionKind), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.PendingClientCommand, result.Status);
        Assert.Single(result.RuntimeCommands);
        Assert.Equal(actionKind, result.RuntimeCommands[0].CommandKind);
    }

    [Theory]
    [InlineData("counter")]
    [InlineData("incrementCounter")]
    [InlineData("gauge")]
    public async Task MetricsFamily_ExecutesWithoutRuntimeCommands(string actionKind)
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback(actionKind);

        var result = await executor.ExecuteAsync(Context(actionKind, actionKind switch
        {
            "incrementCounter" => new { metricName = "orders.increment" },
            _ => new { metricName = "orders.metric", valueExpression = new { raw = "1" } }
        }), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Empty(result.RuntimeCommands);
    }

    [Fact]
    public async Task ConnectorBackedExecutor_FailsWhenCapabilityMissing()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback("webServiceCall");

        var result = await executor.ExecuteAsync(Context("webServiceCall"), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.ConnectorRequired, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeConnectorRequired, result.Error?.Code);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.ConnectorCapability == MicroflowRuntimeConnectorCapability.SoapWebService);
    }

    [Fact]
    public async Task ConnectorBackedDescriptors_AllRequireCapabilityWhenMissing()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var connectorBacked = MicroflowActionExecutorRegistry.BuiltInDescriptors()
            .Where(descriptor => descriptor.RuntimeCategory == MicroflowActionRuntimeCategory.ConnectorBacked)
            .ToArray();

        Assert.NotEmpty(connectorBacked);
        foreach (var descriptor in connectorBacked)
        {
            var executor = registry.GetOrFallback(descriptor.ActionKind);
            var result = await executor.ExecuteAsync(Context(descriptor.ActionKind), CancellationToken.None);

            Assert.Equal(MicroflowActionExecutionStatus.ConnectorRequired, result.Status);
            Assert.Equal(RuntimeErrorCode.RuntimeConnectorRequired, result.Error?.Code);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.ConnectorCapability == descriptor.ConnectorCapability);
        }
    }

    [Fact]
    public async Task ConnectorBackedExecutor_DelegatesToConnectorRegistryWhenCapabilityAvailable()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback("webServiceCall");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = MicroflowRuntimeConnectorCapability.SoapWebService,
                OutputJson = "{\"provider\":\"soap\",\"ok\":true}"
            });

        var result = await executor.ExecuteAsync(Context("webServiceCall", connectorRegistry: connectorRegistry), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Single(connectorRegistry.Requests);
        Assert.Equal(MicroflowRuntimeConnectorCapability.SoapWebService, connectorRegistry.Requests[0].Capability);
        Assert.Single(result.ConnectorRequests);
        Assert.True(result.OutputJson?.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ConnectorBackedExecutor_MapsConnectorExecutionFailureToFailed()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback("webServiceCall");
        var connectorRegistry = new StubConnectorRegistry(
            hasCapability: true,
            result: new MicroflowConnectorExecutionResult
            {
                Success = false,
                Capability = MicroflowRuntimeConnectorCapability.SoapWebService,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeUnknownError,
                    Message = "SOAP connector execution failed."
                }
            });

        var result = await executor.ExecuteAsync(Context("webServiceCall", connectorRegistry: connectorRegistry), CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnknownError, result.Error?.Code);
        Assert.True(result.ShouldStopRun);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.ConnectorCapability == MicroflowRuntimeConnectorCapability.SoapWebService);
    }

    [Fact]
    public async Task ConnectorBackedExecutor_Failure_MapsLatestSoapFault_For_WebServiceCall()
    {
        var registry = new MicroflowActionExecutorRegistry();
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
                    Message = "Remote SOAP fault."
                },
                LatestSoapFault = JsonSerializer.SerializeToElement(new
                {
                    faultCode = "Client.Invalid",
                    faultString = "Payload invalid"
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });

        var result = await executor.ExecuteAsync(
            Context("webServiceCall", new { endpoint = "https://soap.test/service", operation = "SubmitOrder" }, connectorRegistry),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.True(result.LatestSoapFault.HasValue);
        Assert.Equal("Client.Invalid", result.LatestSoapFault.Value.GetProperty("faultCode").GetString());
        Assert.Equal("Payload invalid", result.LatestSoapFault.Value.GetProperty("faultString").GetString());
    }

    [Fact]
    public async Task RuntimeConnectorRegistry_CanRegisterAndExecuteConnector()
    {
        var registry = new MicroflowRuntimeConnectorRegistry();
        registry.Register(new PassThroughRuntimeConnector(MicroflowRuntimeConnectorCapability.SoapWebService));

        Assert.True(registry.HasCapability(MicroflowRuntimeConnectorCapability.SoapWebService));
        Assert.Contains(MicroflowRuntimeConnectorCapability.SoapWebService, registry.ListEnabledCapabilities());

        var result = await registry.ExecuteAsync(
            new MicroflowConnectorExecutionRequest
            {
                Capability = MicroflowRuntimeConnectorCapability.SoapWebService,
                ActionKind = "webServiceCall",
                ObjectId = "node-1"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MicroflowRuntimeConnectorCapability.SoapWebService, result.Capability);
    }

    [Fact]
    public void R3ProductionExecutors_AreSupportedAndNoLongerModeledOnly()
    {
        var byKind = MicroflowActionExecutorRegistry.BuiltInDescriptors()
            .ToDictionary(descriptor => descriptor.ActionKind, StringComparer.OrdinalIgnoreCase);

        var expectedExecutors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rollback"] = "RollbackObjectActionExecutor",
            ["cast"] = "CastObjectActionExecutor",
            ["listOperation"] = "ListOperationActionExecutor",
            ["counter"] = "MetricsActionExecutor",
            ["incrementCounter"] = "MetricsActionExecutor",
            ["gauge"] = "MetricsActionExecutor"
        };

        foreach (var actionKind in expectedExecutors.Keys)
        {
            Assert.True(byKind.TryGetValue(actionKind, out var descriptor), $"Missing descriptor for {actionKind}");
            Assert.Equal(MicroflowActionRuntimeCategory.ServerExecutable, descriptor.RuntimeCategory);
            Assert.Equal(MicroflowActionSupportLevel.Supported, descriptor.SupportLevel);
            Assert.Equal(expectedExecutors[actionKind], descriptor.Executor);
            Assert.True(descriptor.RealExecution);
        }
    }

    [Fact]
    public async Task UnknownAction_UsesFallbackUnsupportedExecutor()
    {
        var registry = new MicroflowActionExecutorRegistry();
        var executor = registry.GetOrFallback("notARealAction");

        var result = await executor.ExecuteAsync(Context("notARealAction"), CancellationToken.None);

        Assert.Equal(MicroflowActionRuntimeCategory.ExplicitUnsupported, executor.Category);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedAction, result.Error?.Code);
    }

    private static MicroflowActionExecutionContext Context(
        string actionKind,
        object? config = null,
        IMicroflowRuntimeConnectorRegistry? connectorRegistry = null)
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-action-executor-test",
            SchemaId = "schema-action-executor-test",
            StartNodeId = "start"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-action-executor-test",
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

        public Task<MicroflowConnectorExecutionResult> ExecuteAsync(
            MicroflowConnectorExecutionRequest request,
            CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }

    private sealed class PassThroughRuntimeConnector : IMicroflowRuntimeConnector
    {
        public PassThroughRuntimeConnector(string capability)
        {
            Capability = capability;
        }

        public string Capability { get; }

        public bool Enabled => true;

        public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
            => Task.FromResult(new MicroflowConnectorExecutionResult
            {
                Success = true,
                Capability = Capability,
                OutputJson = "{\"ok\":true}"
            });
    }
}
