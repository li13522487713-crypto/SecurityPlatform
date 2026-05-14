using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions.Database;
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
        if (string.Equals(actionKind, "queryExternalDatabase", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<DatabaseQueryActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "declareLocalVariable", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<DeclareLocalVariableActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

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

        if (string.Equals(actionKind, "rollback", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<RollbackObjectActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "cast", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<CastObjectActionExecutor>();
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

        if (string.Equals(actionKind, "listOperation", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ListOperationActionExecutor>();
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

        if (string.Equals(actionKind, "webServiceCall", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<SoapWebServiceActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "importXml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "exportXml", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<XmlMappingActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "generateDocument", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<DocumentGenerationActionExecutor>();
            if (specialized is not null)
            {
                executor = specialized;
                return true;
            }
        }

        if (string.Equals(actionKind, "createExternalObject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "changeExternalObject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "sendExternalObject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "deleteExternalObject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "externalObject", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<ExternalObjectActionExecutor>();
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

        if (string.Equals(actionKind, "callWorkflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "changeWorkflowState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "completeUserTask", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "applyJumpToOption", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "generateJumpToOptions", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "retrieveWorkflowActivityRecords", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "retrieveWorkflowContext", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "retrieveWorkflows", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "showUserTaskPage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "showWorkflowAdminPage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "lockWorkflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "unlockWorkflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "notifyWorkflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "workflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "workflowAction", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<WorkflowActionExecutor>();
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

        if (string.Equals(actionKind, "counter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "incrementCounter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "gauge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionKind, "metrics", StringComparison.OrdinalIgnoreCase))
        {
            var specialized = _serviceProvider?.GetService<MetricsActionExecutor>();
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
            Server("rollback", "RollbackAction", "object", "RollbackObjectActionExecutor",
                producesVariables: false, producesTransaction: true,
                reason: "Rollback reverts staged runtime object changes through UnitOfWork/transaction tracking and reports reverted/noop/invalidated status."),
            Server("cast", "CastObjectAction", "object", "CastObjectActionExecutor",
                producesVariables: true, producesTransaction: false,
                reason: "Cast validates runtime object metadata inheritance, entity access, strict/allowNull modes, and binds a typed object variable."),

            Server("createList", "CreateListAction", "list", "CreateListActionExecutor",
                producesVariables: true, producesTransaction: false,
                reason: "CreateList aligns canonical authoring payload, creates mutable list variables, and supports empty or expression-derived initial items."),
            Server("changeList", "ChangeListAction", "list", "ChangeListActionExecutor",
                producesVariables: true, producesTransaction: false,
                reason: "ChangeList aligns canonical authoring payload and executes add/addAll/addRange/remove/removeAll/removeWhere/clear/set against runtime list variables."),
            Server("listOperation", "ListOperationAction", "list", "ListOperationActionExecutor",
                producesVariables: true, producesTransaction: false,
                reason: "ListOperation aligns canonical authoring payload and implements set/scalar/transform operations without mutating input lists."),
            Server("aggregateList", "AggregateListAction", "list", "AggregateListActionExecutor",
                producesVariables: true, producesTransaction: false,
                reason: "AggregateList aligns canonical authoring payload and executes count/sum/average/min/max with empty-list handling and typed outputs."),
            Server("filterList", "FilterListAction", "list", "FilterListActionExecutor", producesVariables: true, producesTransaction: false, reason: "Filter List evaluates a per-item expression and produces a new list variable."),
            Server("sortList", "SortListAction", "list", "SortListActionExecutor", producesVariables: true, producesTransaction: false, reason: "Sort List orders items by a primitive member and produces a new list variable."),

            Server("createVariable", "CreateVariableAction", "variable", "CreateVariableActionExecutor", producesVariables: true, producesTransaction: false),
            Server("declareLocalVariable", "DeclareLocalVariableAction", "variable", "DeclareLocalVariableActionExecutor", producesVariables: true, producesTransaction: false, reason: "DeclareLocalVariable supports scope(local/global), dataType, source(literal/expression/reference/empty), replaces createVariable."),
            Server("changeVariable", "ChangeVariableAction", "variable", "ChangeVariableActionExecutor", producesVariables: true, producesTransaction: false),
            Server("break", "BreakAction", "loop", "BreakActionExecutor", producesVariables: false, producesTransaction: false),
            Server("continue", "ContinueAction", "loop", "ContinueActionExecutor", producesVariables: false, producesTransaction: false),

            Server("callMicroflow", "CallMicroflowAction", "call", "CallMicroflowActionExecutor", producesVariables: true, producesTransaction: false, reason: "Local call semantics are represented in testRun with recursion guard and return variable preview."),
            Connector("callJavaAction", "CallJavaAction", "call", "JavaActionExecutor", MicroflowRuntimeConnectorCapability.JavaAction, "Java action requires a plugin host connector."),
            Command("callJavaScriptAction", "CallJavaScriptAction", "call", "CallJavaScriptActionExecutor", "callJavaScriptAction"),
            Command("callNanoflow", "CallNanoflowAction", "call", "CallNanoflowActionExecutor", "callNanoflow"),

            Server("restCall", "RestCallAction", "integration", "RestCallActionExecutor", producesVariables: true, producesTransaction: false, reason: "Runtime can build REST requests, enforce HTTP policy, block external calls by default, and use real HTTP when allowRealHttp is enabled."),
            // SOAP/XML 现已走专用 executor；仍保留 connector capability gate（缺能力时返回 RUNTIME_CONNECTOR_REQUIRED）。
            Connector("webServiceCall", "WebServiceCallAction", "integration", "SoapWebServiceActionExecutor", MicroflowRuntimeConnectorCapability.SoapWebService, "SOAP/WSDL execution requires web service connector."),
            Connector("importXml", "ImportXmlAction", "integration", "XmlMappingActionExecutor", MicroflowRuntimeConnectorCapability.XmlImportMapping, "XML import mapping requires mapping connector."),
            Connector("exportXml", "ExportXmlAction", "integration", "XmlMappingActionExecutor", MicroflowRuntimeConnectorCapability.XmlExportMapping, "XML export mapping requires mapping connector."),
            Connector("callExternalAction", "CallExternalAction", "integration", "ConnectorBackedActionExecutor:callExternalAction", "external.action", "External action requires connector capability."),
            Connector("restOperationCall", "RestOperationCallAction", "integration", "ConnectorBackedActionExecutor:restOperationCall", MicroflowRuntimeConnectorCapability.RestRealHttp, "REST operation calls require real HTTP connector capability."),
            Server("queryExternalDatabase", "QueryExternalDatabaseAction", "integration", "DatabaseQueryActionExecutor", producesVariables: true, producesTransaction: false, reason: "DatabaseQueryActionExecutor executes parameterized SQL against DatabaseCenter sources (AiDatabase/TenantDataSource/External) with variable placeholder substitution."),

            Command("showPage", "ShowPageAction", "client", "ShowPageActionExecutor", "showPage"),
            Command("showHomePage", "ShowHomePageAction", "client", "ShowHomePageActionExecutor", "showHomePage"),
            Command("showMessage", "ShowMessageAction", "client", "ShowMessageActionExecutor", "showMessage"),
            Command("closePage", "ClosePageAction", "client", "ClosePageActionExecutor", "closePage"),
            Command("validationFeedback", "ValidationFeedbackAction", "client", "ValidationFeedbackActionExecutor", "validationFeedback"),
            Command("downloadFile", "DownloadFileAction", "client", "DownloadFileActionExecutor", "downloadFile"),
            Command("synchronize", "SynchronizeAction", "client", "SynchronizeActionExecutor", "synchronize"),

            Server("logMessage", "LogMessageAction", "logging", "LogMessageActionExecutor", producesVariables: false, producesTransaction: false),
            Server("throwException", "ThrowExceptionAction", "errorHandling", "ThrowExceptionActionExecutor", producesVariables: false, producesTransaction: false, reason: "Server-side throwException stops the run with a structured RuntimeError."),
            Connector("generateDocument", "GenerateDocumentAction", "documentGeneration", "DocumentGenerationActionExecutor", MicroflowRuntimeConnectorCapability.DocumentGeneration, "Document generation is deprecated and requires document connector.", supportLevel: MicroflowActionSupportLevel.Deprecated),

            Server("counter", "MetricsCounterAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, reason: "Counter emits a structured runtime metric entry."),
            Server("incrementCounter", "MetricsIncrementCounterAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, reason: "IncrementCounter emits a structured runtime metric entry with value 1."),
            Server("gauge", "MetricsGaugeAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, reason: "Gauge emits a structured runtime metric entry."),
            Server("metrics", "MicroflowGenericAction", "metrics", "MetricsActionExecutor", producesVariables: false, producesTransaction: false, reason: "Legacy metrics alias emits a structured runtime metric entry."),

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

            Connector("deleteExternalObject", "DeleteExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object delete requires external object connector capability."),
            Connector("sendExternalObject", "SendExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object send requires external object connector capability."),

            Connector("externalObject", "MicroflowGenericAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "Legacy external object action requires connector."),
            Connector("connectorCall", "MicroflowGenericAction", "integration", "ExternalActionExecutor", "external.action", "Legacy connectorCall requires connector."),
            Connector("externalConnectorCall", "MicroflowGenericAction", "integration", "ExternalActionExecutor", "external.action", "Legacy externalConnectorCall requires connector."),
            Connector("sendEmail", "SendEmailAction", "communication", "EmailActionExecutor", MicroflowRuntimeConnectorCapability.EmailSend, "Email delivery requires email connector capability."),
            Connector("sendNotification", "SendNotificationAction", "communication", "NotificationActionExecutor", MicroflowRuntimeConnectorCapability.NotificationSend, "Notification delivery requires notification connector capability."),
            Connector("publishMessage", "PublishMessageAction", "messaging", "MessageBrokerActionExecutor", MicroflowRuntimeConnectorCapability.MessagePublish, "Message publishing requires broker connector capability."),
            Connector("consumeMessage", "ConsumeMessageAction", "messaging", "MessageBrokerActionExecutor", MicroflowRuntimeConnectorCapability.MessageReceive, "Message consumption requires broker connector capability."),
            Connector("callODataAction", "CallODataAction", "odata", "ODataActionExecutor", MicroflowRuntimeConnectorCapability.ODataAction, "OData action calls require OData connector capability."),
            Connector("retrieveODataObject", "RetrieveODataObjectAction", "odata", "ODataObjectExecutor", MicroflowRuntimeConnectorCapability.ODataRetrieve, "OData object retrieval requires OData connector capability."),
            Connector("commitODataObject", "CommitODataObjectAction", "odata", "ODataObjectExecutor", MicroflowRuntimeConnectorCapability.ODataCommit, "OData object commit requires OData connector capability."),
            Connector("deleteODataObject", "DeleteODataObjectAction", "odata", "ODataObjectExecutor", MicroflowRuntimeConnectorCapability.ODataDelete, "OData object delete requires OData connector capability."),
            Connector("retrieveFileDocument", "RetrieveFileDocumentAction", "fileDocument", "FileDocumentActionExecutor", MicroflowRuntimeConnectorCapability.FileDocumentRead, "File document retrieval requires file storage connector capability."),
            Connector("storeFileDocument", "StoreFileDocumentAction", "fileDocument", "FileDocumentActionExecutor", MicroflowRuntimeConnectorCapability.FileDocumentWrite, "File document storage requires file storage connector capability."),
            Connector("exportFileDocument", "ExportFileDocumentAction", "fileDocument", "FileDocumentActionExecutor", MicroflowRuntimeConnectorCapability.FileDocumentRead, "File document export requires file storage connector capability."),
            Connector("importFileDocument", "ImportFileDocumentAction", "fileDocument", "FileDocumentActionExecutor", MicroflowRuntimeConnectorCapability.FileDocumentWrite, "File document import requires file storage connector capability."),
            Connector("createExternalObject", "CreateExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object create requires external object connector capability."),
            Connector("changeExternalObject", "ChangeExternalObjectAction", "externalObject", "ExternalObjectActionExecutor", MicroflowRuntimeConnectorCapability.ExternalObjectCrud, "External object update requires external object connector capability."),
            Command("javascriptAction", "MicroflowGenericAction", "call", "CallJavaScriptActionExecutor", "callJavaScriptAction"),
            Command("nanoflowCall", "MicroflowGenericAction", "call", "CallNanoflowActionExecutor", "callNanoflow"),
            Command("nanoflowCallAction", "MicroflowGenericAction", "call", "CallNanoflowActionExecutor", "callNanoflow"),
            Command("nanoflowOnlySynchronize", "MicroflowGenericAction", "client", "SynchronizeActionExecutor", "synchronize"),
            Command("synchronizeToDevice", "MicroflowGenericAction", "client", "SynchronizeActionExecutor", "synchronize")
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

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
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
            return new MicroflowActionExecutionResult
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
            };
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
                return new MicroflowActionExecutionResult
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
                };
            }

            var connectorResult = await context.ConnectorRegistry.ExecuteAsync(request, ct);
            if (!connectorResult.Success)
            {
                var error = connectorResult.Error ?? new MicroflowRuntimeErrorDto
                {
                    Code = Descriptor.ErrorCode ?? RuntimeErrorCode.RuntimeConnectorRequired,
                    Message = Descriptor.Reason,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                };
                var code = string.IsNullOrWhiteSpace(error.Code) ? RuntimeErrorCode.RuntimeUnknownError : error.Code;
                var status = string.Equals(code, RuntimeErrorCode.RuntimeConnectorRequired, StringComparison.OrdinalIgnoreCase)
                    ? MicroflowActionExecutionStatus.ConnectorRequired
                    : MicroflowActionExecutionStatus.Failed;
                return new MicroflowActionExecutionResult
                {
                    Status = status,
                    Error = error with
                    {
                        Code = code,
                        ObjectId = error.ObjectId ?? context.ObjectId,
                        ActionId = error.ActionId ?? context.ActionId
                    },
                    ConnectorRequests = [request],
                    Logs = connectorResult.Logs,
                    LatestSoapFault = ResolveLatestSoapFault(ActionKind, connectorResult),
                    Diagnostics =
                    [
                        new MicroflowActionExecutionDiagnostic
                        {
                            Code = code,
                            Severity = "error",
                            Message = error.Message ?? Descriptor.Reason,
                            ActionKind = ActionKind,
                            ObjectId = context.ObjectId,
                            ActionId = context.ActionId,
                            ConnectorCapability = request.Capability
                        }
                    ],
                    ShouldContinueNormalFlow = false,
                    ShouldEnterErrorHandler = true,
                    ShouldStopRun = true,
                    Message = error.Message ?? Descriptor.Reason
                };
            }

            return new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                OutputJson = TryParseConnectorOutputJson(connectorResult.OutputJson) ?? JsonSerializer.SerializeToElement(new
                {
                    actionKind = ActionKind,
                    connectorCapability = request.Capability,
                    outputPreview = Descriptor.Reason
                }, JsonOptions),
                OutputPreview = Descriptor.Reason,
                ConnectorRequests = [request],
                Logs = connectorResult.Logs,
                Message = Descriptor.Reason
            };
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
            return new MicroflowActionExecutionResult
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
            };
        }

        return new MicroflowActionExecutionResult
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
        };
    }

    private static JsonElement? TryParseConnectorOutputJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { rawOutput = json }, JsonOptions);
        }
    }

    private static JsonElement? ResolveLatestSoapFault(string actionKind, MicroflowConnectorExecutionResult connectorResult)
    {
        if (!string.Equals(actionKind, "webServiceCall", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (connectorResult.LatestSoapFault.HasValue)
        {
            return connectorResult.LatestSoapFault.Value;
        }

        if (TryParseSoapFault(connectorResult.OutputJson, out var fault))
        {
            return fault;
        }

        if (connectorResult.Error is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(new
        {
            faultCode = connectorResult.Error.Code,
            faultString = connectorResult.Error.Message,
            detail = connectorResult.Error.Details
        }, JsonOptions);
    }

    private static bool TryParseSoapFault(string? json, out JsonElement fault)
    {
        fault = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && (root.TryGetProperty("faultCode", out _) || root.TryGetProperty("faultString", out _) || root.TryGetProperty("detail", out _)))
            {
                fault = root.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }
}

public sealed class MicroflowRuntimeConnectorRegistry : IMicroflowRuntimeConnectorRegistry
{
    private readonly Dictionary<string, IMicroflowRuntimeConnector> _connectors = new(StringComparer.OrdinalIgnoreCase);

    public MicroflowRuntimeConnectorRegistry(IEnumerable<IMicroflowRuntimeConnector>? connectors = null)
    {
        if (connectors is null)
        {
            return;
        }

        foreach (var connector in connectors)
        {
            Register(connector);
        }
    }

    public void Register(IMicroflowRuntimeConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        if (string.IsNullOrWhiteSpace(connector.Capability))
        {
            return;
        }

        _connectors[connector.Capability] = connector;
    }

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
