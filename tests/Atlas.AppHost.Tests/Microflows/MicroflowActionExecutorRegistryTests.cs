using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowActionExecutorRegistryTests
{
    private static readonly string[] FrontendActionKinds =
    [
        "retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "cast",
        "aggregateList", "createList", "changeList", "listOperation",
        "createVariable", "changeVariable",
        "callMicroflow", "callJavaAction", "callJavaScriptAction", "callNanoflow",
        "restCall", "webServiceCall", "importXml", "exportXml", "callExternalAction", "restOperationCall",
        "closePage", "downloadFile", "showHomePage", "showMessage", "showPage", "validationFeedback", "synchronize",
        "logMessage",
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
    public void R3ProductionExecutors_AreSupportedAndNoLongerModeledOnly()
    {
        var byKind = MicroflowActionExecutorRegistry.BuiltInDescriptors()
            .ToDictionary(descriptor => descriptor.ActionKind, StringComparer.OrdinalIgnoreCase);

        var expectedExecutors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rollback"] = "RollbackObjectActionExecutor",
            ["cast"] = "CastObjectActionExecutor",
            ["listOperation"] = "ListOperationActionExecutor"
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

    private static MicroflowActionExecutionContext Context(string actionKind)
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
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry()
        };
    }
}
