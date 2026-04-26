import type {
  MicroflowAction,
  MicroflowActionCategory,
  MicroflowActionKind,
  MicroflowActivityConfig,
  MicroflowActivityType,
  MicroflowDataType,
  MicroflowErrorHandlingType,
  MicroflowExpression,
  MicroflowNodeAvailability,
  MicroflowPropertyTabKey,
  MicroflowRuntimeNodeDto,
  MicroflowValidationIssue
} from "../schema/types";

export type MicroflowRegistryAvailability = MicroflowNodeAvailability | "hidden";

export interface MicroflowActionRegistryItem {
  key: MicroflowActionKind;
  kind: MicroflowActionKind;
  actionKind: MicroflowActionKind;
  legacyActivityType: MicroflowActivityType;
  officialType: string;
  title: string;
  titleZh: string;
  description: string;
  category: MicroflowActionCategory;
  group?: string;
  iconKey: string;
  colorToken?: string;
  availability: MicroflowRegistryAvailability;
  availabilityReason?: string;
  keywords: string[];
  defaultCaption: string;
  defaultConfig: MicroflowActivityConfig;
  outputSpec?: Array<{ id: string; name: string; dataType: MicroflowDataType; source: string }>;
  inputSpec?: Array<{ id: string; title: string; dataType?: MicroflowDataType; required?: boolean }>;
  supportsErrorHandling: boolean;
  supportedErrorHandlingTypes: MicroflowErrorHandlingType[];
  propertyTabs: MicroflowPropertyTabKey[];
  createsActionActivity: true;
  createAction: (input: { id: string; config?: Partial<MicroflowActivityConfig>; caption?: string }) => MicroflowAction;
  validate: (action: MicroflowAction) => MicroflowValidationIssue[];
  toRuntimeDto: (action: MicroflowAction) => MicroflowRuntimeNodeDto;
}

export type MicroflowActionRegistryEntry = MicroflowActionRegistryItem;

function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function issue(code: string, message: string, actionId: string, severity: MicroflowValidationIssue["severity"] = "error"): MicroflowValidationIssue {
  return { id: `${code}:${actionId}`, code, severity, message, actionId };
}

function availabilityReason(availability: MicroflowRegistryAvailability): string | undefined {
  if (availability === "nanoflowOnlyDisabled") {
    return "该动作仅支持 Nanoflow，Microflow 编辑器中禁用。";
  }
  if (availability === "requiresConnector") {
    return "需要启用对应 Connector 后才能运行。";
  }
  if (availability === "deprecated") {
    return "兼容历史模型保留，新模型不建议继续使用。";
  }
  if (availability === "beta") {
    return "Beta 能力，运行时契约可能继续演进。";
  }
  if (availability === "hidden") {
    return "内部保留能力，默认不在面板中显示。";
  }
  return undefined;
}

function baseAction(
  item: Pick<MicroflowActionRegistryItem, "actionKind" | "officialType" | "category" | "iconKey" | "availability" | "availabilityReason" | "description">,
  id: string,
  caption?: string
) {
  return {
    id,
    officialType: item.officialType,
    kind: item.actionKind,
    caption,
    errorHandlingType: "rollback" as const,
    documentation: item.description,
    editor: {
      category: item.category,
      iconKey: item.iconKey,
      availability: item.availability === "hidden" ? "supported" as const : item.availability,
      availabilityReason: item.availabilityReason
    }
  };
}

function createConcreteAction(item: MicroflowActionRegistryItem, id: string, config: Partial<MicroflowActivityConfig> = {}, caption?: string): MicroflowAction {
  const base = baseAction(item, id, caption);
  if (item.actionKind === "retrieve") {
    return {
      ...base,
      kind: "retrieve",
      officialType: "Microflows$RetrieveAction",
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? "result"),
      retrieveSource: {
        kind: "database",
        officialType: "Microflows$DatabaseRetrieveSource",
        entityQualifiedName: typeof config.entity === "string" ? config.entity : null,
        xPathConstraint: null,
        sortItemList: { items: [] },
        range: { kind: "first", officialType: "Microflows$ConstantRange", value: "first" }
      }
    };
  }
  if (item.actionKind === "createObject") {
    return {
      ...base,
      kind: "createObject",
      officialType: "Microflows$CreateObjectAction",
      entityQualifiedName: String(config.entity ?? "System.Object"),
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? "newObject"),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false }
    };
  }
  if (item.actionKind === "changeMembers") {
    return {
      ...base,
      kind: "changeMembers",
      officialType: "Microflows$ChangeMembersAction",
      changeVariableName: String(config.objectVariableName ?? "object"),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false },
      validateObject: true
    };
  }
  if (item.actionKind === "commit") {
    return {
      ...base,
      kind: "commit",
      officialType: "Microflows$CommitAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? "object"),
      withEvents: true,
      refreshInClient: false
    };
  }
  if (item.actionKind === "delete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? "object"),
      withEvents: true,
      deleteBehavior: "deleteOnly"
    };
  }
  if (item.actionKind === "rollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? "object"),
      refreshInClient: false
    };
  }
  if (item.actionKind === "callMicroflow") {
    return {
      ...base,
      kind: "callMicroflow",
      officialType: "Microflows$MicroflowCallAction",
      targetMicroflowId: String(config.targetMicroflowId ?? ""),
      parameterMappings: [],
      returnValue: { storeResult: false },
      callMode: "sync"
    };
  }
  if (item.actionKind === "createVariable") {
    return {
      ...base,
      kind: "createVariable",
      officialType: "Microflows$CreateVariableAction",
      variableName: String(config.variableName ?? "variable"),
      dataType: { kind: "unknown", reason: "registry default" },
      initialValue: expression("", { kind: "unknown", reason: "registry default" })
    };
  }
  if (item.actionKind === "changeVariable") {
    return {
      ...base,
      kind: "changeVariable",
      officialType: "Microflows$ChangeVariableAction",
      variableName: String(config.variableName ?? "variable"),
      valueExpression: expression("")
    };
  }
  if (item.actionKind === "restCall") {
    return {
      ...base,
      kind: "restCall",
      officialType: "Microflows$RestCallAction",
      request: {
        method: config.method ?? "GET",
        urlExpression: expression(String(config.url ?? ""), { kind: "string" }),
        headers: [],
        queryParameters: [],
        body: { kind: "none" }
      },
      response: { handling: { kind: "ignore" } },
      timeoutSeconds: 30
    };
  }
  if (item.actionKind === "logMessage") {
    return {
      ...base,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      level: "info",
      logNodeName: "Microflow",
      template: { text: "Log message", arguments: [] },
      includeContextVariables: false,
      includeTraceId: true
    };
  }
  return { ...base, kind: item.actionKind } as MicroflowAction;
}

function validateAction(action: MicroflowAction): MicroflowValidationIssue[] {
  if (action.kind === "retrieve" && !("outputVariableName" in action)) {
    return [issue("MF_ACTION_RETRIEVE_OUTPUT_REQUIRED", "Retrieve action requires outputVariableName.", action.id)];
  }
  if (action.kind === "restCall" && !action.request.method) {
    return [issue("MF_ACTION_REST_METHOD_REQUIRED", "REST call action requires method.", action.id)];
  }
  if (action.kind === "logMessage" && !action.level) {
    return [issue("MF_ACTION_LOG_LEVEL_REQUIRED", "Log message action requires level.", action.id)];
  }
  if (action.kind === "callMicroflow" && !action.targetMicroflowId) {
    return [issue("MF_ACTION_CALL_TARGET_EMPTY", "Call microflow target is not selected yet.", action.id, "warning")];
  }
  if (action.kind === "createVariable" && !action.variableName) {
    return [issue("MF_ACTION_VARIABLE_NAME_REQUIRED", "Create variable action requires variableName.", action.id)];
  }
  return [];
}

function action(input: {
  key: MicroflowActionKind;
  legacyActivityType: MicroflowActivityType;
  officialType: string;
  title: string;
  titleZh: string;
  description: string;
  category: MicroflowActionCategory;
  availability?: MicroflowRegistryAvailability;
  defaultConfig?: Partial<MicroflowActivityConfig>;
  supportsErrorHandling?: boolean;
}): MicroflowActionRegistryItem {
  const availability = input.availability ?? "supported";
  const supportsErrorHandling = input.supportsErrorHandling ?? !["client", "logging", "metrics", "variable"].includes(input.category);
  const item = {
    key: input.key,
    kind: input.key,
    actionKind: input.key,
    legacyActivityType: input.legacyActivityType,
    officialType: input.officialType,
    title: input.title,
    titleZh: input.titleZh,
    description: input.description,
    category: input.category,
    iconKey: input.key,
    availability,
    availabilityReason: availabilityReason(availability),
    keywords: [input.key, input.title, input.titleZh, input.description, input.category],
    defaultCaption: input.titleZh,
    defaultConfig: {
      activityType: input.legacyActivityType,
      activityCategory: input.category,
      supportsErrorFlow: supportsErrorHandling,
      errorHandling: supportsErrorHandling ? { mode: "rollback", errorVariableName: "latestError" } : undefined,
      ...input.defaultConfig
    },
    supportsErrorHandling,
    supportedErrorHandlingTypes: supportsErrorHandling ? ["rollback", "customWithRollback", "customWithoutRollback"] : [],
    propertyTabs: supportsErrorHandling ? ["properties", "documentation", "errorHandling", "output", "advanced"] : ["properties", "documentation", "output"],
    createsActionActivity: true as const,
    createAction: ({ id, config, caption }) => createConcreteAction(item, id, { ...item.defaultConfig, ...config }, caption),
    validate: validateAction,
    toRuntimeDto: createdAction => ({
      nodeId: createdAction.id,
      type: "activity",
      kind: "activity",
      activityType: input.legacyActivityType,
      title: createdAction.caption ?? input.title,
      config: {
        kind: "action",
        actionKind: createdAction.kind,
        officialType: createdAction.officialType,
        unsupported: true
      }
    })
  };
  return item;
}

export const defaultMicroflowActionRegistry: MicroflowActionRegistryItem[] = [
  action({ key: "retrieve", legacyActivityType: "objectRetrieve", officialType: "Microflows$RetrieveAction", title: "Retrieve Object(s)", titleZh: "检索对象", description: "Retrieves one or more objects from database or association.", category: "object", defaultConfig: { entity: "System.Object", resultVariableName: "result" } }),
  action({ key: "createObject", legacyActivityType: "objectCreate", officialType: "Microflows$CreateObjectAction", title: "Create Object", titleZh: "创建对象", description: "Creates an object instance and stores it in a variable.", category: "object", defaultConfig: { entity: "System.Object", objectVariableName: "newObject" } }),
  action({ key: "changeMembers", legacyActivityType: "objectChange", officialType: "Microflows$ChangeMembersAction", title: "Change Object", titleZh: "修改对象", description: "Changes member values on an existing object.", category: "object", defaultConfig: { objectVariableName: "object" } }),
  action({ key: "commit", legacyActivityType: "objectCommit", officialType: "Microflows$CommitAction", title: "Commit Object(s)", titleZh: "提交对象", description: "Persists one object or object list.", category: "object", defaultConfig: { objectVariableName: "object" } }),
  action({ key: "delete", legacyActivityType: "objectDelete", officialType: "Microflows$DeleteAction", title: "Delete Object(s)", titleZh: "删除对象", description: "Deletes one object or object list.", category: "object", defaultConfig: { objectVariableName: "object" } }),
  action({ key: "rollback", legacyActivityType: "objectRollback", officialType: "Microflows$RollbackAction", title: "Rollback Object", titleZh: "回滚对象", description: "Rolls back uncommitted object changes.", category: "object", defaultConfig: { objectVariableName: "object" } }),
  action({ key: "cast", legacyActivityType: "objectCast", officialType: "Microflows$CastAction", title: "Cast Object", titleZh: "转换对象", description: "Casts a generalized object to a specialization.", category: "object", defaultConfig: { objectVariableName: "object" } }),
  action({ key: "aggregateList", legacyActivityType: "listAggregate", officialType: "Microflows$AggregateListAction", title: "Aggregate List", titleZh: "列表聚合", description: "Aggregates a list with count, sum, average, min, or max.", category: "list", defaultConfig: { listVariableName: "items" } }),
  action({ key: "createList", legacyActivityType: "listCreate", officialType: "Microflows$CreateListAction", title: "Create List", titleZh: "创建列表", description: "Creates an empty typed list variable.", category: "list", defaultConfig: { listVariableName: "items" } }),
  action({ key: "changeList", legacyActivityType: "listChange", officialType: "Microflows$ChangeListAction", title: "Change List", titleZh: "修改列表", description: "Adds, removes, clears, or replaces list contents.", category: "list", defaultConfig: { listVariableName: "items" } }),
  action({ key: "listOperation", legacyActivityType: "listOperation", officialType: "Microflows$ListOperationAction", title: "List Operation", titleZh: "列表操作", description: "Filters, sorts, combines, or selects list items.", category: "list", defaultConfig: { listVariableName: "items" } }),
  action({ key: "callMicroflow", legacyActivityType: "callMicroflow", officialType: "Microflows$MicroflowCallAction", title: "Call Microflow", titleZh: "调用微流", description: "Calls another microflow with parameter mapping.", category: "call", defaultConfig: { targetMicroflowId: "" } }),
  action({ key: "callJavaAction", legacyActivityType: "callJavaAction", officialType: "Microflows$JavaActionCallAction", title: "Call Java Action", titleZh: "调用 Java 动作", description: "Calls a server-side Java action.", category: "call" }),
  action({ key: "callJavaScriptAction", legacyActivityType: "callJavaScriptAction", officialType: "Microflows$JavaScriptActionCallAction", title: "Call JavaScript Action", titleZh: "调用 JavaScript 动作", description: "Nanoflow-only JavaScript action, disabled in Microflow.", category: "call", availability: "nanoflowOnlyDisabled" }),
  action({ key: "callNanoflow", legacyActivityType: "callNanoflow", officialType: "Microflows$NanoflowCallAction", title: "Call Nanoflow", titleZh: "调用纳流", description: "Nanoflow-only call node, disabled in Microflow.", category: "call", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false }),
  action({ key: "createVariable", legacyActivityType: "variableCreate", officialType: "Microflows$CreateVariableAction", title: "Create Variable", titleZh: "创建变量", description: "Creates a local microflow variable.", category: "variable", defaultConfig: { variableName: "variable" }, supportsErrorHandling: false }),
  action({ key: "changeVariable", legacyActivityType: "variableChange", officialType: "Microflows$ChangeVariableAction", title: "Change Variable", titleZh: "修改变量", description: "Changes the value of an existing variable.", category: "variable", defaultConfig: { variableName: "variable" }, supportsErrorHandling: false }),
  action({ key: "closePage", legacyActivityType: "closePage", officialType: "Microflows$ClosePageAction", title: "Close Page", titleZh: "关闭页面", description: "Closes the current or last opened page.", category: "client", supportsErrorHandling: false }),
  action({ key: "downloadFile", legacyActivityType: "downloadFile", officialType: "Microflows$DownloadFileAction", title: "Download File", titleZh: "下载文件", description: "Downloads a file document in the browser.", category: "client", supportsErrorHandling: false }),
  action({ key: "showHomePage", legacyActivityType: "showHomePage", officialType: "Microflows$ShowHomePageAction", title: "Show Home Page", titleZh: "显示首页", description: "Navigates the user to the home page.", category: "client", supportsErrorHandling: false }),
  action({ key: "showMessage", legacyActivityType: "showMessage", officialType: "Microflows$ShowMessageAction", title: "Show Message", titleZh: "显示消息", description: "Displays a blocking or non-blocking message.", category: "client", supportsErrorHandling: false }),
  action({ key: "showPage", legacyActivityType: "showPage", officialType: "Microflows$ShowPageAction", title: "Show Page", titleZh: "显示页面", description: "Opens a page for the current user.", category: "client", supportsErrorHandling: false }),
  action({ key: "validationFeedback", legacyActivityType: "validationFeedback", officialType: "Microflows$ValidationFeedbackAction", title: "Validation Feedback", titleZh: "验证反馈", description: "Shows validation feedback under a page field.", category: "client", supportsErrorHandling: false }),
  action({ key: "synchronize", legacyActivityType: "synchronize", officialType: "Microflows$SynchronizeAction", title: "Synchronize", titleZh: "同步", description: "Nanoflow-only synchronize action, disabled in Microflow.", category: "client", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false }),
  action({ key: "restCall", legacyActivityType: "callRest", officialType: "Microflows$RestCallAction", title: "Call REST Service", titleZh: "调用 REST 服务", description: "Calls a REST endpoint with request and response mapping.", category: "integration", defaultConfig: { method: "GET", url: "" } }),
  action({ key: "webServiceCall", legacyActivityType: "callWebService", officialType: "Microflows$WebServiceCallAction", title: "Call Web Service", titleZh: "调用 Web Service", description: "Calls an imported SOAP/Web Service.", category: "integration" }),
  action({ key: "importXml", legacyActivityType: "importWithMapping", officialType: "Microflows$ImportXmlAction", title: "Import Mapping", titleZh: "导入映射", description: "Imports XML or JSON through an import mapping.", category: "integration" }),
  action({ key: "exportXml", legacyActivityType: "exportWithMapping", officialType: "Microflows$ExportXmlAction", title: "Export Mapping", titleZh: "导出映射", description: "Exports objects through an export mapping.", category: "integration" }),
  action({ key: "callExternalAction", legacyActivityType: "callExternalAction", officialType: "Microflows$CallExternalAction", title: "Call External Action", titleZh: "调用外部动作", description: "Calls an external action from a connector.", category: "integration", availability: "requiresConnector" }),
  action({ key: "restOperationCall", legacyActivityType: "sendRestRequestBeta", officialType: "Microflows$RestOperationCallAction", title: "REST Operation Call", titleZh: "REST 操作调用", description: "Calls an operation from a consumed REST service document.", category: "integration", availability: "beta" }),
  action({ key: "logMessage", legacyActivityType: "logMessage", officialType: "Microflows$LogMessageAction", title: "Log Message", titleZh: "记录日志", description: "Writes an application log entry.", category: "logging", supportsErrorHandling: false }),
  action({ key: "generateDocument", legacyActivityType: "generateDocument", officialType: "Microflows$GenerateDocumentAction", title: "Generate Document", titleZh: "生成文档", description: "Generates a document from a template for legacy compatibility.", category: "documentGeneration", availability: "deprecated" }),
  action({ key: "metric", legacyActivityType: "counter", officialType: "Microflows$MetricAction", title: "Metric", titleZh: "指标", description: "Emits a custom runtime metric.", category: "metrics", supportsErrorHandling: false }),
  action({ key: "mlModelCall", legacyActivityType: "callMlModel", officialType: "Microflows$MLModelCallAction", title: "Call ML Model", titleZh: "调用 ML 模型", description: "Calls an ML model mapping.", category: "mlKit" }),
  action({ key: "workflowAction", legacyActivityType: "callWorkflow", officialType: "Microflows$WorkflowAction", title: "Workflow Action", titleZh: "工作流动作", description: "Runs a Mendix workflow-related action.", category: "workflow" }),
  action({ key: "externalObjectAction", legacyActivityType: "sendExternalObject", officialType: "Microflows$ExternalObjectAction", title: "External Object Action", titleZh: "外部对象动作", description: "Sends, updates, or deletes an external object.", category: "externalObject", availability: "requiresConnector" })
];

export const microflowActionRegistryByKind = new Map(defaultMicroflowActionRegistry.map(entry => [entry.kind, entry]));
export const microflowActionRegistryByActivityType = new Map(defaultMicroflowActionRegistry.map(entry => [entry.legacyActivityType, entry]));
