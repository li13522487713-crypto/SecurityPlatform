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

    [Fact]
    public async Task GenericOutputAlias_DoesNotProduceMissingOutputDiagnostic()
    {
        var navigator = CreateNavigator(new StubExecutor("generateDocument", succeed: true, writeVariable: true));
        var plan = CreatePlan("generateDocument", outputVariable: "invoiceDoc", outputField: "outputFileDocumentVariableName");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.DoesNotContain(result.Diagnostics.Items, d => d.Code == "RUNTIME_ACTION_OUTPUT_MISSING");
    }

    [Fact]
    public async Task RestCall_StatusCodeAlias_DoesNotProduceMissingOutputDiagnostic()
    {
        var navigator = CreateNavigator(new StubExecutor("restCall", succeed: true, writeVariable: true));
        var plan = CreatePlan("restCall", outputVariable: "statusCode", outputField: "statusCodeVariableName");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.DoesNotContain(result.Diagnostics.Items, d => d.Code == "RUNTIME_ACTION_OUTPUT_MISSING");
    }

    [Fact]
    public async Task CreateList_ListVariableName_Alias_DoesNotProduceMissingOutputDiagnostic()
    {
        var navigator = CreateNavigator(new StubExecutor("createList", succeed: true, writeVariable: true));
        var plan = CreatePlan("createList", outputVariable: "items", outputField: "listVariableName");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.DoesNotContain(result.Diagnostics.Items, d => d.Code == "RUNTIME_ACTION_OUTPUT_MISSING");
    }

    [Fact]
    public async Task Cast_OutputVariable_Alias_DoesNotProduceMissingOutputDiagnostic()
    {
        var navigator = CreateNavigator(new StubExecutor("cast", succeed: true, writeVariable: true));
        var plan = CreatePlan("cast", outputVariable: "memberResult", outputField: "outputVariable");

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.DoesNotContain(result.Diagnostics.Items, d => d.Code == "RUNTIME_ACTION_OUTPUT_MISSING");
    }

    [Fact]
    public async Task ErrorEvent_MessageExpression_Uses_LatestError_In_FlowNavigator()
    {
        var navigator = CreateNavigator(new StubExecutor("throwException", succeed: false, writeVariable: false, errorCode: "BIZ_FAIL", errorMessage: "primary path failed"));
        var plan = CreateErrorEventPlan(new
        {
            raw = new
            {
                error = new
                {
                    sourceVariableName = "$latestError",
                    messageExpression = "$latestError/message + ' (wrapped)'"
                }
            }
        });

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.Equal(MicroflowNavigationStatus.Failed, result.Status);
        Assert.Equal("BIZ_FAIL", result.Error?.Code);
        Assert.Equal("primary path failed (wrapped)", result.Error?.Message);
        Assert.Contains(result.TraceFrames, frame => frame.ObjectId == "error" && frame.Status == MicroflowNavigationStepStatus.Failed);
    }

    [Fact]
    public async Task ErrorEvent_SourceVariable_Rethrows_Original_ErrorCode_In_FlowNavigator()
    {
        var navigator = CreateNavigator(new StubExecutor("throwException", succeed: false, writeVariable: false, errorCode: "DOWNSTREAM_FAIL", errorMessage: "downstream exploded"));
        var plan = CreateErrorEventPlan(new
        {
            raw = new
            {
                error = new
                {
                    sourceVariableName = "$latestError"
                }
            }
        });

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.Equal(MicroflowNavigationStatus.Failed, result.Status);
        Assert.Equal("DOWNSTREAM_FAIL", result.Error?.Code);
        Assert.Equal("downstream exploded", result.Error?.Message);
    }

    [Fact]
    public async Task ErrorEvent_MessageExpression_Uses_LatestHttpResponse_Members_In_FlowNavigator()
    {
        var navigator = CreateNavigator(new StubExecutor("throwException", succeed: false, writeVariable: false, errorCode: RuntimeErrorCode.RuntimeRestCallFailed, errorMessage: "rest request failed"));
        var plan = CreateErrorEventPlan(new
        {
            raw = new
            {
                error = new
                {
                    sourceVariableName = "$latestError",
                    messageExpression = "if $latestHttpResponse/statusCode = 500 then 'handled rest error' else 'unexpected'"
                }
            }
        });

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.Equal(MicroflowNavigationStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeRestCallFailed, result.Error?.Code);
        Assert.Equal("handled rest error", result.Error?.Message);
    }

    [Fact]
    public async Task ErrorEvent_MessageExpression_Uses_LatestSoapFault_Members_In_FlowNavigator()
    {
        var navigator = CreateNavigator(new StubExecutor(
            "webServiceCall",
            succeed: false,
            writeVariable: false,
            errorCode: "SOAP_REMOTE_FAULT",
            errorMessage: "soap request failed",
            latestSoapFault: JsonSerializer.SerializeToElement(new
            {
                faultCode = "Server.Validation",
                faultString = "Order invalid"
            })));
        var plan = CreateSoapErrorEventPlan(new
        {
            raw = new
            {
                error = new
                {
                    sourceVariableName = "$latestError",
                    messageExpression = "$latestSoapFault/faultCode + ': ' + $latestSoapFault/faultString"
                }
            }
        });

        var result = await navigator.NavigateAsync(plan, new MicroflowNavigationOptions { IncludeDiagnostics = true }, CancellationToken.None);

        Assert.Equal(MicroflowNavigationStatus.Failed, result.Status);
        Assert.Equal("SOAP_REMOTE_FAULT", result.Error?.Code);
        Assert.Equal("Server.Validation: Order invalid", result.Error?.Message);
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

    private static MicroflowExecutionPlan CreatePlan(string actionKind, string? outputVariable, string outputField = "outputVariableName")
    {
        var config = outputVariable is null
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.SerializeToElement(new Dictionary<string, string?> { [outputField] = outputVariable });
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

    private static MicroflowExecutionPlan CreateErrorEventPlan(object errorConfig)
    {
        MicroflowExecutionFlow[] flows =
        [
            new() { FlowId = "f1", OriginObjectId = "start", DestinationObjectId = "action" },
            new() { FlowId = "f2-err", OriginObjectId = "action", DestinationObjectId = "error", EdgeKind = "errorHandler", ControlFlow = "errorHandler", IsErrorHandler = true }
        ];

        return new MicroflowExecutionPlan
        {
            StartNodeId = "start",
            Nodes =
            [
                new() { ObjectId = "start", Kind = "startEvent" },
                new() { ObjectId = "action", Kind = "actionActivity", ActionKind = "throwException", SupportLevel = MicroflowRuntimeSupportLevel.Supported, ConfigJson = JsonSerializer.SerializeToElement(new { }) },
                new() { ObjectId = "error", Kind = "errorEvent", ConfigJson = JsonSerializer.SerializeToElement(errorConfig) }
            ],
            Flows = flows,
            NormalFlows = [flows[0]],
            ErrorHandlerFlows = [flows[1]]
        };
    }

    private static MicroflowExecutionPlan CreateSoapErrorEventPlan(object errorConfig)
    {
        MicroflowExecutionFlow[] flows =
        [
            new() { FlowId = "f1", OriginObjectId = "start", DestinationObjectId = "action" },
            new() { FlowId = "f2-err", OriginObjectId = "action", DestinationObjectId = "error", EdgeKind = "errorHandler", ControlFlow = "errorHandler", IsErrorHandler = true }
        ];

        return new MicroflowExecutionPlan
        {
            StartNodeId = "start",
            Nodes =
            [
                new() { ObjectId = "start", Kind = "startEvent" },
                new() { ObjectId = "action", Kind = "actionActivity", ActionKind = "webServiceCall", SupportLevel = MicroflowRuntimeSupportLevel.Supported, ConfigJson = JsonSerializer.SerializeToElement(new { endpoint = "https://soap.test/service", operation = "SubmitOrder" }) },
                new() { ObjectId = "error", Kind = "errorEvent", ConfigJson = JsonSerializer.SerializeToElement(errorConfig) }
            ],
            Flows = flows,
            NormalFlows = [flows[0]],
            ErrorHandlerFlows = [flows[1]]
        };
    }

    private sealed class StubExecutor(string kind, bool succeed, bool writeVariable, string errorCode = RuntimeErrorCode.RuntimeUnknownError, string errorMessage = "failed", JsonElement? latestSoapFault = null) : IMicroflowActionExecutor
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
                    Error = new MicroflowRuntimeErrorDto { Code = errorCode, Message = errorMessage },
                    LatestSoapFault = latestSoapFault
                });
            }

            var outputName = ResolveOutputVariableName(context.ActionConfig);
            if (writeVariable && context.VariableStore is not null && !string.IsNullOrWhiteSpace(outputName))
            {
                var name = outputName!;
                var raw = JsonSerializer.Serialize(new { ok = true, kind });
                context.VariableStore.Define(new MicroflowVariableDefinition { Name = name, DataTypeJson = "{}", Value = new MicroflowRuntimeVariableValue { Name = name, RawValueJson = raw, DataTypeJson = "{}", SourceKind = MicroflowVariableSourceKind.ActionOutput } });
            }

            return Task.FromResult(new MicroflowActionExecutionResult { Status = MicroflowActionExecutionStatus.Success, OutputJson = JsonSerializer.SerializeToElement(new { done = true }) });
        }

        private string? ResolveOutputVariableName(JsonElement config)
        {
            if (config.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return ReadString(config, "outputVariableName")
                ?? ReadString(config, "outputVariable")
                ?? ReadString(config, "outputListVariableName")
                ?? ReadString(config, "resultVariableName")
                ?? ReadString(config, "outputWorkflowVariableName")
                ?? ReadString(config, "outputFileDocumentVariableName")
                ?? ReadString(config, "returnVariableName")
                ?? ReadString(config, "statusCodeVariableName")
                ?? ReadString(config, "headersVariableName")
                ?? ReadActionSpecificOutputAlias(kind, config)
                ?? ReadStringByPath(config, "returnValue", "outputVariableName");
        }

        private static string? ReadActionSpecificOutputAlias(string actionKind, JsonElement config)
            => actionKind is "createList" or "retrieveWorkflows" or "retrieveWorkflowActivityRecords"
                ? ReadString(config, "listVariableName")
                : null;

        private static string? ReadString(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string? ReadStringByPath(JsonElement element, params string[] path)
        {
            var current = element;
            foreach (var part in path)
            {
                if (!current.TryGetProperty(part, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }
    }
}
