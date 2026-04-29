using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class MicroflowActionExecutorRegistry : IMicroflowActionExecutorRegistry
{
    private readonly Dictionary<string, IMicroflowActionExecutor> _executors = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider? _serviceProvider;
    private readonly IMicroflowActionExecutor _fallback = new ConfiguredMicroflowActionExecutor(Descriptor(
        "unknown",
        "MicroflowGenericAction",
        "unknown",
        MicroflowActionRuntimeCategory.ExplicitUnsupported,
        MicroflowActionSupportLevel.Unsupported,
        "FallbackUnsupportedActionExecutor",
        null,
        RuntimeErrorCode.RuntimeUnsupportedAction,
        "Unknown action kind is explicitly unsupported.",
        realExecution: false,
        producesVariables: false,
        producesTransaction: false,
        producesRuntimeCommand: false));

    public MicroflowActionExecutorRegistry(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
        foreach (var descriptor in BuiltInDescriptors())
        {
            Register(new ConfiguredMicroflowActionExecutor(descriptor));
        }
    }

    public void Register(IMicroflowActionExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executors[executor.ActionKind] = executor;
    }

    public bool TryGet(string? actionKind, out IMicroflowActionExecutor executor)
    {
        if (string.Equals(actionKind, "createVariable", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CreateVariableActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "changeVariable", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ChangeVariableActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "break", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<BreakActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "continue", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ContinueActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "retrieve", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<RetrieveObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "createObject", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CreateObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "changeMembers", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ChangeObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "commit", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CommitObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "delete", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<DeleteObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "createList", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CreateListActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "changeList", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ChangeListActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "aggregateList", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<AggregateListActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "callMicroflow", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CallMicroflowActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "restCall", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<RestCallActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "logMessage", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<LogMessageActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "throwException", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ThrowExceptionActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "filterList", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<FilterListActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "sortList", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<SortListActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(actionKind) && _executors.TryGetValue(actionKind, out var resolved))
        {
            executor = resolved;
            return true;
        }

        executor = _fallback;
        return false;
    }

    public IMicroflowActionExecutor GetOrFallback(string? actionKind)
        => TryGet(actionKind, out var executor) ? executor : executor;

    public string GetSupportLevel(string? actionKind)
        => GetOrFallback(actionKind).SupportLevel;

    public string GetCategory(string? actionKind)
        => GetOrFallback(actionKind).Category;

    public IReadOnlyList<MicroflowActionExecutorDescriptor> ListAll()
        => _executors.Values
            .OfType<ConfiguredMicroflowActionExecutor>()
            .Select(executor => executor.Descriptor)
            .OrderBy(descriptor => descriptor.RegistryCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.ActionKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public MicroflowActionExecutorCoverageDiagnostic ValidateCoverage(IEnumerable<string> actionKinds)
    {
        var expected = actionKinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missing = expected.Where(kind => !_executors.ContainsKey(kind)).ToArray();
        return new MicroflowActionExecutorCoverageDiagnostic
        {
            Covered = missing.Length == 0,
            MissingActionKinds = missing,
            CoveredCount = expected.Length - missing.Length,
            ExpectedCount = expected.Length
        };
    }

    public void EnsureEveryActionKindCovered(IEnumerable<string> actionKinds)
    {
        var coverage = ValidateCoverage(actionKinds);
        if (!coverage.Covered)
        {
            throw new InvalidOperationException($"Microflow action executor coverage is incomplete: {string.Join(", ", coverage.MissingActionKinds)}");
        }
    }

    public static IReadOnlyList<string> BuiltInActionKinds => BuiltInDescriptors()
        .Select(descriptor => descriptor.ActionKind)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static IReadOnlyList<MicroflowActionExecutorDescriptor> BuiltInDescriptors()
        =>
        [
            Server("retrieve", "RetrieveAction", "object", "RetrieveActionExecutor", producesVariables: true, producesTransaction: false, reason: "testRun uses the runtime object store; publishedRun requires objectStore.crud for real DB retrieval."),
            Server("createObject", "CreateObjectAction", "object", "CreateObjectActionExecutor", producesVariables: true, producesTransaction: true),
            Server("changeMembers", "ChangeMembersAction", "object", "ChangeMembersActionExecutor", producesVariables: true, producesTransaction: true),
            Server("commit", "CommitAction", "object", "CommitActionExecutor", producesVariables: false, producesTransaction: true),
            Server("delete", "DeleteAction", "object", "DeleteActionExecutor", producesVariables: false, producesTransaction: true),
            // P1-2: Rollback / Cast / ListOperation 当前由 ConfiguredMicroflowActionExecutor
            // 返回 Success + reason，没有真实事务回滚 / 类型转换 / 列表 mutation；
            // SupportLevel 标 ModeledOnlyConverted，Reason 显式说明，以便前端
            // toolbox tooltip 与 verify-microflow-runtime-coverage 矩阵识别。
            Server("rollback", "RollbackAction", "object", "ConfiguredMicroflowActionExecutor",
                producesVariables: false, producesTransaction: true,
                supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted,
                reason: "Rollback 当前由 ConfiguredMicroflowActionExecutor 返回 success（无真实事务回滚）；真实 RollbackObjectActionExecutor 待 P1 后续轮次补齐。"),
            Server("cast", "CastObjectAction", "object", "ConfiguredMicroflowActionExecutor",
                producesVariables: true, producesTransaction: false,
                supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted,
                reason: "Cast 当前由 ConfiguredMicroflowActionExecutor 返回 success；真实 CastObjectActionExecutor（按 metadata 校验继承/实现关系，失败返回 RUNTIME_TYPE_MISMATCH）待 P1 后续轮次补齐。"),

            Server("createList", "CreateListAction", "list", "CreateListActionExecutor", producesVariables: true, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted),
            Server("changeList", "ChangeListAction", "list", "ChangeListActionExecutor", producesVariables: true, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted),
            Server("listOperation", "ListOperationAction", "list", "ConfiguredMicroflowActionExecutor",
                producesVariables: true, producesTransaction: false,
                supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted,
                reason: "ListOperation 当前由 ConfiguredMicroflowActionExecutor 返回 success；真实 add/remove/clear/contains/insert/distinct executor 待 P1 后续轮次补齐。"),
            Server("aggregateList", "AggregateListAction", "list", "AggregateListActionExecutor", producesVariables: true, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted),
            Server("filterList", "FilterListAction", "list", "FilterListActionExecutor", producesVariables: true, producesTransaction: false, reason: "Filter List evaluates a per-item expression and produces a new list variable."),
            Server("sortList", "SortListAction", "list", "SortListActionExecutor", producesVariables: true, producesTransaction: false, reason: "Sort List orders items by a primitive member and produces a new list variable."),

            Server("createVariable", "CreateVariableAction", "variable", "CreateVariableActionExecutor", producesVariables: true, producesTransaction: false),
            Server("changeVariable", "ChangeVariableAction", "variable", "ChangeVariableActionExecutor", producesVariables: true, producesTransaction: false),
            Server("break", "BreakAction", "loop", "BreakActionExecutor", producesVariables: false, producesTransaction: false),
            Server("continue", "ContinueAction", "loop", "ContinueActionExecutor", producesVariables: false, producesTransaction: false),

            Server("callMicroflow", "CallMicroflowAction", "call", "CallMicroflowActionExecutor", producesVariables: true, producesTransaction: false, reason: "Local call semantics are represented in testRun with recursion guard and return variable preview."),
            Connector("callJavaAction", "CallJavaAction", "call", "JavaActionExecutor", MicroflowRuntimeConnectorCapability.JavaAction, "Java action requires a plugin host connector."),
            Unsupported("callJavaScriptAction", "CallJavaScriptAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Nanoflow JavaScript actions must run on the client."),
            Unsupported("callNanoflow", "CallNanoflowAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Nanoflow calls cannot execute inside server Microflow runtime."),

            Server("restCall", "RestCallAction", "integration", "RestCallActionExecutor", producesVariables: true, producesTransaction: false, reason: "Runtime can build REST requests, enforce HTTP policy, block external calls by default, and use real HTTP when allowRealHttp is enabled."),
            // P1-2: 这些 Connector 描述符的 Executor 字段保留 connector 实现规划名，
            // 但实际运行由 ConfiguredMicroflowActionExecutor 在 connector 缺失时返回
            // RUNTIME_CONNECTOR_REQUIRED；等真实 *ActionExecutor 落地后会替换 DI。
            Connector("webServiceCall", "WebServiceCallAction", "integration", "ConnectorBackedActionExecutor:webServiceCall", MicroflowRuntimeConnectorCapability.SoapWebService, "SOAP/WSDL execution requires web service connector."),
            Connector("importXml", "ImportXmlAction", "integration", "ConnectorBackedActionExecutor:importXml", MicroflowRuntimeConnectorCapability.XmlImportMapping, "XML import mapping requires mapping connector."),
            Connector("exportXml", "ExportXmlAction", "integration", "ConnectorBackedActionExecutor:exportXml", MicroflowRuntimeConnectorCapability.XmlExportMapping, "XML export mapping requires mapping connector."),
            Connector("callExternalAction", "CallExternalAction", "integration", "ConnectorBackedActionExecutor:callExternalAction", "external.action", "External action requires connector capability."),
            Connector("restOperationCall", "RestOperationCallAction", "integration", "ConnectorBackedActionExecutor:restOperationCall", MicroflowRuntimeConnectorCapability.RestRealHttp, "REST operation calls require real HTTP connector capability."),

            Command("showPage", "ShowPageAction", "client", "ShowPageActionExecutor", "showPage"),
            Command("showHomePage", "ShowHomePageAction", "client", "ShowHomePageActionExecutor", "showHomePage"),
            Command("showMessage", "ShowMessageAction", "client", "ShowMessageActionExecutor", "showMessage"),
            Command("closePage", "ClosePageAction", "client", "ClosePageActionExecutor", "closePage"),
            Command("validationFeedback", "ValidationFeedbackAction", "client", "ValidationFeedbackActionExecutor", "validationFeedback"),
            Command("downloadFile", "DownloadFileAction", "client", "DownloadFileActionExecutor", "downloadFile"),
            Unsupported("synchronize", "SynchronizeAction", "client", MicroflowActionSupportLevel.NanoflowOnly, "Synchronize is nanoflow/client-device only."),

            Server("logMessage", "LogMessageAction", "logging", "LogMessageActionExecutor", producesVariables: false, producesTransaction: false),
            Server("throwException", "ThrowExceptionAction", "errorHandling", "ThrowExceptionActionExecutor", producesVariables: false, producesTransaction: false, reason: "Server-side throwException stops the run with a structured RuntimeError."),
            Connector("generateDocument", "GenerateDocumentAction", "documentGeneration", "DocumentGenerationExecutor", MicroflowRuntimeConnectorCapability.DocumentGeneration, "Document generation is deprecated and requires document connector.", supportLevel: MicroflowActionSupportLevel.Deprecated),

            Server("counter", "MetricsCounterAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted, reason: "Metrics fallback writes runtime log when metrics connector is absent."),
            Server("incrementCounter", "MetricsIncrementCounterAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted, reason: "Metrics fallback writes runtime log when metrics connector is absent."),
            Server("gauge", "MetricsGaugeAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted, reason: "Metrics fallback writes runtime log when metrics connector is absent."),
            Server("metrics", "MicroflowGenericAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, supportLevel: MicroflowActionSupportLevel.ModeledOnlyConverted, reason: "Legacy metrics action writes runtime log fallback."),

            Connector("mlModelCall", "MlModelCallAction", "mlKit", "MLModelCallExecutor", MicroflowRuntimeConnectorCapability.MlModel, "ML model execution requires ML connector."),

            Connector("applyJumpToOption", "ApplyJumpToOptionAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("callWorkflow", "CallWorkflowAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("changeWorkflowState", "ChangeWorkflowStateAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("completeUserTask", "CompleteUserTaskAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("generateJumpToOptions", "GenerateJumpToOptionsAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("retrieveWorkflowActivityRecords", "RetrieveWorkflowActivityRecordsAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("retrieveWorkflowContext", "RetrieveWorkflowContextAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("retrieveWorkflows", "RetrieveWorkflowsAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("showUserTaskPage", "ShowUserTaskPageAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("showWorkflowAdminPage", "ShowWorkflowAdminPageAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("lockWorkflow", "LockWorkflowAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("unlockWorkflow", "UnlockWorkflowAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("notifyWorkflow", "NotifyWorkflowAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Workflow runtime connector required."),
            Connector("workflow", "MicroflowGenericAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Legacy workflow action requires workflow connector."),
            Connector("workflowAction", "MicroflowGenericAction", "workflow", "WorkflowActionExecutor", MicroflowRuntimeConnectorCapability.WorkflowAction, "Legacy workflowAction requires workflow connector."),

            Connector("deleteExternalObject", "DeleteExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object CRUD connector required."),
            Connector("sendExternalObject", "SendExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object CRUD connector required."),

            Connector("externalObject", "MicroflowGenericAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "Legacy external object action requires connector."),
            Connector("connectorCall", "MicroflowGenericAction", "integration", "ExternalActionExecutor", "external.action", "Legacy connectorCall requires connector."),
            Connector("externalConnectorCall", "MicroflowGenericAction", "integration", "ExternalActionExecutor", "external.action", "Legacy externalConnectorCall requires connector."),
            Unsupported("javascriptAction", "MicroflowGenericAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Legacy JavaScript action is nanoflow-only."),
            Unsupported("nanoflowCall", "MicroflowGenericAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Legacy nanoflowCall is nanoflow-only."),
            Unsupported("nanoflowCallAction", "MicroflowGenericAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Legacy nanoflowCallAction is nanoflow-only."),
            Unsupported("nanoflowOnlySynchronize", "MicroflowGenericAction", "client", MicroflowActionSupportLevel.NanoflowOnly, "Legacy synchronize is nanoflow-only.")
        ];

    private static MicroflowActionExecutorDescriptor Server(
        string actionKind,
        string schemaType,
        string category,
        string executor,
        bool producesVariables,
        bool producesTransaction,
        string supportLevel = MicroflowActionSupportLevel.Supported,
        string? reason = null)
        => Descriptor(actionKind, schemaType, category, MicroflowActionRuntimeCategory.ServerExecutable, supportLevel, executor, null, null, reason ?? "Server executable action.", realExecution: true, producesVariables, producesTransaction, producesRuntimeCommand: false);

    private static MicroflowActionExecutorDescriptor Command(string actionKind, string schemaType, string category, string executor, string commandKind)
        => Descriptor(actionKind, schemaType, category, MicroflowActionRuntimeCategory.RuntimeCommand, MicroflowActionSupportLevel.ModeledOnlyConverted, executor, MicroflowRuntimeConnectorCapability.ClientCommand, null, $"Server returns RuntimeCommand '{commandKind}' for client handling.", realExecution: true, producesVariables: false, producesTransaction: false, producesRuntimeCommand: true);

    private static MicroflowActionExecutorDescriptor Connector(
        string actionKind,
        string schemaType,
        string category,
        string executor,
        string capability,
        string reason,
        string supportLevel = MicroflowActionSupportLevel.RequiresConnector)
        => Descriptor(actionKind, schemaType, category, MicroflowActionRuntimeCategory.ConnectorBacked, supportLevel, executor, capability, RuntimeErrorCode.RuntimeConnectorRequired, reason, realExecution: false, producesVariables: false, producesTransaction: false, producesRuntimeCommand: false);

    private static MicroflowActionExecutorDescriptor Unsupported(string actionKind, string schemaType, string category, string supportLevel, string reason)
        => Descriptor(actionKind, schemaType, category, MicroflowActionRuntimeCategory.ExplicitUnsupported, supportLevel, "ExplicitUnsupportedActionExecutor", null, RuntimeErrorCode.RuntimeUnsupportedAction, reason, realExecution: false, producesVariables: false, producesTransaction: false, producesRuntimeCommand: false);

    private static MicroflowActionExecutorDescriptor Descriptor(
        string actionKind,
        string schemaType,
        string category,
        string runtimeCategory,
        string supportLevel,
        string executor,
        string? connectorCapability,
        string? errorCode,
        string reason,
        bool realExecution,
        bool producesVariables,
        bool producesTransaction,
        bool producesRuntimeCommand)
        => new()
        {
            ActionKind = actionKind,
            SchemaType = schemaType,
            RegistryCategory = category,
            RuntimeCategory = runtimeCategory,
            SupportLevel = supportLevel,
            Executor = executor,
            ConnectorCapability = connectorCapability,
            ErrorCode = errorCode,
            Reason = reason,
            RealExecution = realExecution,
            ProducesVariables = producesVariables,
            ProducesTransaction = producesTransaction,
            ProducesRuntimeCommand = producesRuntimeCommand,
            VerifyCovered = true
        };
}

public sealed class ConfiguredMicroflowActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ConfiguredMicroflowActionExecutor(MicroflowActionExecutorDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public MicroflowActionExecutorDescriptor Descriptor { get; }

    public string ActionKind => Descriptor.ActionKind;

    public string Category => Descriptor.RuntimeCategory;

    public string SupportLevel => Descriptor.SupportLevel;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Category == MicroflowActionRuntimeCategory.RuntimeCommand)
        {
            var command = new MicroflowRuntimeCommand
            {
                CommandKind = ActionKind,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId,
                PayloadJson = context.ActionConfig.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : context.ActionConfig.GetRawText(),
                Message = Descriptor.Reason
            };
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.PendingClientCommand,
                OutputJson = JsonSerializer.SerializeToElement(new
                {
                    actionKind = ActionKind,
                    executorCategory = Category,
                    supportLevel = SupportLevel,
                    runtimeCommands = new[] { command },
                    outputPreview = Descriptor.Reason
                }, JsonOptions),
                OutputPreview = Descriptor.Reason,
                RuntimeCommands = [command],
                ShouldContinueNormalFlow = true,
                Message = Descriptor.Reason
            });
        }

        if (Category == MicroflowActionRuntimeCategory.ConnectorBacked)
        {
            var request = new MicroflowConnectorExecutionRequest
            {
                Capability = Descriptor.ConnectorCapability ?? string.Empty,
                ActionKind = ActionKind,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                PayloadJson = context.ActionConfig.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : context.ActionConfig.GetRawText()
            };
            if (!context.ConnectorRegistry.HasCapability(request.Capability))
            {
                var error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeConnectorRequired,
                    Message = Descriptor.Reason,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    Details = JsonSerializer.Serialize(new { ActionKind, request.Capability }, JsonOptions)
                };
                return Task.FromResult(new MicroflowActionExecutionResult
                {
                    Status = MicroflowActionExecutionStatus.ConnectorRequired,
                    Error = error,
                    ConnectorRequests = [request],
                    Diagnostics =
                    [
                        new MicroflowActionExecutionDiagnostic
                        {
                            Code = RuntimeErrorCode.RuntimeConnectorRequired,
                            Severity = "error",
                            Message = Descriptor.Reason,
                            ActionKind = ActionKind,
                            ObjectId = context.ObjectId,
                            ActionId = context.ActionId,
                            ConnectorCapability = request.Capability
                        }
                    ],
                    ShouldContinueNormalFlow = false,
                    ShouldEnterErrorHandler = true,
                    ShouldStopRun = true,
                    Message = Descriptor.Reason
                });
            }
        }

        if (Category == MicroflowActionRuntimeCategory.ExplicitUnsupported)
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeUnsupportedAction,
                Message = Descriptor.Reason,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = JsonSerializer.Serialize(new { ActionKind, SupportLevel }, JsonOptions)
            };
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Unsupported,
                Error = error,
                Diagnostics =
                [
                    new MicroflowActionExecutionDiagnostic
                    {
                        Code = RuntimeErrorCode.RuntimeUnsupportedAction,
                        Severity = "error",
                        Message = Descriptor.Reason,
                        ActionKind = ActionKind,
                        ObjectId = context.ObjectId,
                        ActionId = context.ActionId
                    }
                ],
                ShouldContinueNormalFlow = false,
                ShouldEnterErrorHandler = true,
                ShouldStopRun = true,
                Message = Descriptor.Reason
            });
        }

        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = JsonSerializer.SerializeToElement(new
            {
                actionKind = ActionKind,
                executorCategory = Category,
                supportLevel = SupportLevel,
                outputPreview = Descriptor.Reason
            }, JsonOptions),
            OutputPreview = Descriptor.Reason
        });
    }
}

public sealed class MicroflowRuntimeConnectorRegistry : IMicroflowRuntimeConnectorRegistry
{
    private readonly Dictionary<string, IMicroflowRuntimeConnector> _connectors = new(StringComparer.OrdinalIgnoreCase);

    public bool HasCapability(string capability)
        => !string.IsNullOrWhiteSpace(capability)
            && _connectors.TryGetValue(capability, out var connector)
            && connector.Enabled;

    public IReadOnlyList<string> ListEnabledCapabilities()
        => _connectors.Values
            .Where(connector => connector.Enabled)
            .Select(connector => connector.Capability)
            .OrderBy(capability => capability, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(MicroflowConnectorExecutionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasCapability(request.Capability))
        {
            return Task.FromResult(new MicroflowConnectorExecutionResult
            {
                Success = false,
                Capability = request.Capability,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeConnectorRequired,
                    Message = $"Connector capability '{request.Capability}' is not enabled.",
                    ObjectId = request.ObjectId,
                    ActionId = request.ActionId
                }
            });
        }

        return _connectors[request.Capability].ExecuteAsync(request, ct);
    }
}
