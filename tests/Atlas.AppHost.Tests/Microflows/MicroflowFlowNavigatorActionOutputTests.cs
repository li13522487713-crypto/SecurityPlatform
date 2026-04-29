using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowFlowNavigatorActionOutputTests
{
    [Fact]
    public async Task Retrieve_Success_WritesRealVariableValue()
    {
        var navigator = CreateNavigator(new StubExecutor("retrieve", succeed: true, writeVariable: true));
        var plan = CreatePlan("retrieve", "result");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions(), CancellationToken.None);

        var actionFrame = result.TraceFrames.Single(x => x.ObjectId == "action");
        Assert.Contains(actionFrame.VariablesSnapshot?.Values ?? [], v => v.Name == "result" && v.RawValue?.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Action_Failed_DoesNotWritePseudoVariable()
    {
        var navigator = CreateNavigator(new StubExecutor("createObject", succeed: false, writeVariable: false));
        var plan = CreatePlan("createObject", "obj");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions(), CancellationToken.None);

        var actionFrame = result.TraceFrames.Single(x => x.ObjectId == "action");
        Assert.DoesNotContain(actionFrame.VariablesSnapshot?.Values ?? [], v => v.Name == "obj");
    }

    [Fact]
    public async Task MissingOutputVariableConfig_ProducesDiagnostic()
    {
        var navigator = CreateNavigator(new StubExecutor("restCall", succeed: true, writeVariable: false));
        var plan = CreatePlan("restCall", outputVariable: null);

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.Contains(result.Diagnostics.Items, d => d.Code == "RUNTIME_ACTION_OUTPUT_MISSING");
    }

    private static MicroflowFlowNavigator CreateNavigator(IMicroflowActionExecutor executor)
    {
        var registry = new MicroflowActionExecutorRegistry();
        registry.Register(executor);
        var expressionEvaluator = new MicroflowExpressionEvaluator();
        var transactionManager = new MicroflowTransactionManager(new SystemMicroflowClock());
        return new MicroflowFlowNavigator(
            new SystemMicroflowClock(),
            expressionEvaluator,
            registry,
            new MicroflowLoopExecutor(expressionEvaluator, registry, transactionManager),
            new MicroflowRuntimeConnectorRegistry());
    }

    private static MicroflowExecutionPlan CreatePlan(string actionKind, string? outputVariable)
    {
        var config = outputVariable is null
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.SerializeToElement(new { outputVariableName = outputVariable });
        MicroflowExecutionFlow[] flows =
        [
            new() { FlowId = "f1", OriginObjectId = "start", DestinationObjectId = "action" },
            new() { FlowId = "f2", OriginObjectId = "action", DestinationObjectId = "end" }
        ];

        return new MicroflowExecutionPlan
        {
            StartNodeId = "start",
            Nodes =
            [
                new() { ObjectId = "start", Kind = "startEvent" },
                new() { ObjectId = "action", Kind = "actionActivity", ActionKind = actionKind, SupportLevel = MicroflowRuntimeSupportLevel.Supported, ConfigJson = JsonSerializer.SerializeToElement(config) },
                new() { ObjectId = "end", Kind = "endEvent" }
            ],
            Flows = flows,
            NormalFlows = flows
        };
    }

    private sealed class StubExecutor(string kind, bool succeed, bool writeVariable) : IMicroflowActionExecutor
    {
        public string ActionKind => kind;
        public string Category => MicroflowActionRuntimeCategory.ServerExecutable;
        public string SupportLevel => MicroflowActionSupportLevel.Supported;

        public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
        {
            if (!succeed)
            {
                return Task.FromResult(new MicroflowActionExecutionResult
                {
                    Status = MicroflowActionExecutionStatus.Failed,
                    Error = new MicroflowRuntimeErrorDto { Code = RuntimeErrorCode.RuntimeUnknownError, Message = "failed" }
                });
            }

            if (writeVariable && context.VariableStore is not null && context.ActionConfig.TryGetProperty("outputVariableName", out var output))
            {
                var name = output.GetString()!;
                var raw = JsonSerializer.Serialize(new { ok = true, kind });
                context.VariableStore.Define(new MicroflowVariableDefinition { Name = name, DataTypeJson = "{}", Value = new MicroflowRuntimeVariableValue { Name = name, RawValueJson = raw, DataTypeJson = "{}", SourceKind = MicroflowVariableSourceKind.ActionOutput } });
            }

            return Task.FromResult(new MicroflowActionExecutionResult { Status = MicroflowActionExecutionStatus.Success, OutputJson = JsonSerializer.SerializeToElement(new { done = true }) });
        }
    }
}
