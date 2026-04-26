import type {
  MicroflowActivityCategory,
  MicroflowActivityConfig,
  MicroflowActivityType,
  MicroflowErrorHandlingType,
  MicroflowActionKind,
  MicroflowNode,
  MicroflowNodeAvailability,
  MicroflowNodeCategory,
  MicroflowNodeKind,
  MicroflowNodeType,
  MicroflowObject,
  MicroflowObjectKind,
  MicroflowPort,
  MicroflowPortCardinality,
  MicroflowPortKind,
  MicroflowPosition,
  MicroflowPropertyFormMetadata,
  MicroflowPropertyTabKey,
  MicroflowRenderMetadata,
  MicroflowRuntimeNodeDto,
  MicroflowValidationIssue
} from "../schema/types";
import { defaultMicroflowActionRegistry, microflowActionRegistryByActivityType, type MicroflowActionRegistryItem } from "./action-registry";

export type MicroflowNodePanelCategoryKey =
  | "events"
  | "decisions"
  | "activities"
  | "loop"
  | "parameters"
  | "annotations";

export type MicroflowNodeGroup = MicroflowActivityCategory;
export type MicroflowNodeCardStatus = "default" | "hover" | "favorite" | "disabled" | "dragging";

export interface MicroflowNodePanelState {
  activeTab: "nodes" | "components" | "templates";
  keyword: string;
  filterKey: MicroflowNodeFilterKey;
  expandedCategories: string[];
  expandedGroups: string[];
}

export interface MicroflowNodeFavoriteState {
  keys: string[];
}

export interface MicroflowNodeDragPayload {
  dragType: "microflow-node";
  nodeType: MicroflowNodeType;
  objectKind: MicroflowObjectKind;
  activityType?: MicroflowActivityType;
  actionKind?: MicroflowActionKind;
  registryKey: string;
  title: string;
  defaultConfig: Record<string, unknown>;
  sourcePanel: "nodes" | "microflow-node-panel";
}

export type MicroflowNodeFilterKey = "all" | "favorites" | "enabled" | MicroflowNodePanelCategoryKey;

export interface MicroflowNodeCategoryDefinition {
  key: MicroflowNodePanelCategoryKey;
  label: string;
  category: MicroflowNodeCategory;
  groups?: Array<{ key: MicroflowNodeGroup; label: string }>;
}

export interface MicroflowNodeIoSpec {
  id: string;
  title: string;
  type: string;
  required?: boolean;
  description?: string;
}

export interface MicroflowNodeRegistryEntry<TConfig extends object = Record<string, unknown>> {
  key?: string;
  type: MicroflowNodeType;
  kind: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  actionKind?: MicroflowActionKind;
  objectKind?: MicroflowObjectKind;
  officialType?: string;
  title: string;
  titleZh: string;
  description: string;
  category: MicroflowNodeCategory;
  activityCategory?: MicroflowActivityCategory;
  group: "Events" | "Decisions" | "Activities" | "Loop" | "Parameters" | "Annotations";
  subgroup?: MicroflowActivityCategory;
  iconKey: string;
  colorToken?: string;
  keywords: string[];
  availability: MicroflowNodeAvailability;
  availabilityReason?: string;
  defaultConfig: TConfig;
  defaultCaption?: string;
  defaultSize?: { width: number; height: number };
  inputs: MicroflowNodeIoSpec[];
  outputs: MicroflowNodeIoSpec[];
  ports: MicroflowPort[];
  supportsErrorHandling: boolean;
  supportsDocumentation?: boolean;
  supportsRuntimeTrace?: boolean;
  supportsValidation?: boolean;
  supportedErrorHandlingTypes: MicroflowErrorHandlingType[];
  propertyTabs: MicroflowPropertyTabKey[];
  enabled: boolean;
  disabledReason?: string;
  documentation: {
    summary: string;
    whenToUse: string;
    examples: string[];
    notes: string[];
  };
  useCases: string[];
  render: MicroflowRenderMetadata;
  propertyForm: MicroflowPropertyFormMetadata;
  canCreate?: boolean;
  createObject?: (input: { id: string; position: MicroflowPosition }) => MicroflowObject;
  validate: (node: MicroflowNode) => MicroflowValidationIssue[];
  toRuntimeDto: (node: MicroflowNode) => MicroflowRuntimeNodeDto;
  fromRuntimeDto: (dto: MicroflowRuntimeNodeDto, position: MicroflowPosition) => MicroflowNode;
}

export type MicroflowNodeRegistryItem<TConfig extends object = Record<string, unknown>> = MicroflowNodeRegistryEntry<TConfig>;

export const microflowNodeCategoryDefinitions: MicroflowNodeCategoryDefinition[] = [
  { key: "events", label: "Events", category: "events" },
  { key: "decisions", label: "Decisions", category: "decisions" },
  {
    key: "activities",
    label: "Activities",
    category: "activities",
    groups: [
      { key: "object", label: "Object" },
      { key: "list", label: "List" },
      { key: "call", label: "Call" },
      { key: "variable", label: "Variable" },
      { key: "client", label: "Client" },
      { key: "integration", label: "Integration" },
      { key: "logging", label: "Logging" },
      { key: "documentGeneration", label: "Document Generation" },
      { key: "metrics", label: "Metrics" },
      { key: "mlKit", label: "ML Kit" },
      { key: "workflow", label: "Workflow" },
      { key: "externalObject", label: "External Object" }
    ]
  },
  { key: "loop", label: "Loop", category: "loop" },
  { key: "parameters", label: "Parameters", category: "parameters" },
  { key: "annotations", label: "Annotations", category: "annotations" }
];

function port(id: string, label: string, direction: MicroflowPort["direction"], kind: MicroflowPortKind, cardinality: MicroflowPortCardinality, edgeTypes: MicroflowPort["edgeTypes"]): MicroflowPort {
  return { id, label, direction, kind, cardinality, edgeTypes };
}

const sequenceIn = port("in", "In", "input", "sequenceIn", "one", ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"]);
const sequenceOut = port("out", "Out", "output", "sequenceOut", "one", ["sequence"]);
const decisionTrue = port("true", "True", "output", "decisionOut", "one", ["decisionCondition"]);
const decisionFalse = port("false", "False", "output", "decisionOut", "one", ["decisionCondition"]);
const objectTypeOut = port("objectType", "Object Type", "output", "objectTypeOut", "oneOrMore", ["objectTypeCondition"]);
const errorOut = port("error", "Error", "output", "errorOut", "zeroOrOne", ["errorHandler"]);
const annotationOut = port("note", "Note", "output", "annotation", "zeroOrMore", ["annotation"]);

function cloneConfig<TConfig extends object>(config: TConfig): TConfig {
  return JSON.parse(JSON.stringify(config)) as TConfig;
}

function baseValidate(node: MicroflowNode): MicroflowValidationIssue[] {
  return node.validation?.disabled ? [{
    id: `MF_NODE_DISABLED:${node.id}`,
    code: "MF_NODE_DISABLED",
    message: "Node validation is disabled.",
    severity: "info",
    nodeId: node.id
  }] : [];
}

function doc(summary: string, whenToUse = summary, notes: string[] = []): MicroflowNodeRegistryEntry["documentation"] {
  return { summary, whenToUse, examples: [], notes };
}

function availabilityReason(availability: MicroflowNodeAvailability): string | undefined {
  if (availability === "nanoflowOnlyDisabled") {
    return "该节点仅支持 Nanoflow，Microflow 编辑器中禁用。";
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
  return undefined;
}

function mapTabs(sections: string[], supportsErrorHandling: boolean): MicroflowPropertyTabKey[] {
  const tabs = new Set<MicroflowPropertyTabKey>(["properties", "documentation"]);
  if (supportsErrorHandling) {
    tabs.add("errorHandling");
  }
  if (sections.includes("Output")) {
    tabs.add("output");
  }
  if (sections.includes("Advanced")) {
    tabs.add("advanced");
  }
  return [...tabs];
}

function createNodeFromRegistry(entry: MicroflowNodeRegistryEntry, id: string, position: MicroflowPosition, title = entry.title): MicroflowNode {
  const base = {
    id,
    type: entry.type,
    kind: entry.kind,
    title,
    titleZh: entry.titleZh,
    description: entry.description,
    category: entry.category,
    position,
    ports: cloneConfig(entry.ports),
    render: entry.render,
    propertyForm: entry.propertyForm,
    availability: entry.availability,
    availabilityReason: entry.availabilityReason,
    documentation: {
      summary: entry.documentation.summary,
      whenToUse: entry.documentation.whenToUse,
      examples: entry.documentation.examples,
      notes: entry.documentation.notes
    }
  };

  return {
    ...base,
    config: cloneConfig(entry.defaultConfig)
  } as unknown as MicroflowNode;
}

function createEntry<TConfig extends object>(entry: Omit<MicroflowNodeRegistryEntry<TConfig>, "keywords" | "inputs" | "outputs" | "enabled" | "disabledReason" | "supportsErrorHandling" | "supportedErrorHandlingTypes" | "propertyTabs" | "validate" | "toRuntimeDto" | "fromRuntimeDto" | "useCases"> & Partial<Pick<MicroflowNodeRegistryEntry<TConfig>, "keywords" | "inputs" | "outputs" | "enabled" | "disabledReason" | "supportsErrorHandling" | "supportedErrorHandlingTypes" | "propertyTabs" | "validate" | "useCases">>): MicroflowNodeRegistryEntry<TConfig> {
  const supportsErrorHandling = entry.supportsErrorHandling ?? entry.ports.some(item => item.kind === "errorOut");
  const disabledReason = entry.disabledReason ?? entry.availabilityReason;
  return {
    ...entry,
    key: entry.activityType ? `action:${actionKindFromActivityType(entry.activityType) ?? entry.activityType}` : entry.type,
    objectKind: objectKindFromRegistryItem(entry),
    officialType: officialTypeFromRegistryItem(entry),
    defaultCaption: entry.titleZh,
    defaultSize: { width: entry.render.width ?? 176, height: entry.render.height ?? 76 },
    keywords: entry.keywords ?? [entry.title, entry.titleZh, entry.description, entry.group, entry.subgroup, entry.iconKey, entry.availability].filter((value): value is string => Boolean(value)),
    enabled: entry.enabled ?? !["requiresConnector", "nanoflowOnlyDisabled"].includes(entry.availability),
    disabledReason,
    inputs: entry.inputs ?? entry.ports.filter(item => item.direction === "input").map(item => ({ id: item.id, title: item.label, type: item.kind })),
    outputs: entry.outputs ?? entry.ports.filter(item => item.direction === "output").map(item => ({ id: item.id, title: item.label, type: item.kind })),
    supportsErrorHandling,
    supportedErrorHandlingTypes: entry.supportedErrorHandlingTypes ?? (supportsErrorHandling ? ["rollback", "customWithRollback", "customWithoutRollback"] : []),
    propertyTabs: entry.propertyTabs ?? mapTabs(entry.propertyForm.sections, supportsErrorHandling),
    supportsDocumentation: true,
    supportsRuntimeTrace: true,
    supportsValidation: true,
    canCreate: entry.enabled ?? !["requiresConnector", "nanoflowOnlyDisabled"].includes(entry.availability),
    useCases: entry.useCases ?? [entry.documentation.whenToUse],
    validate: entry.validate ?? baseValidate,
    toRuntimeDto: node => ({
      nodeId: node.id,
      type: node.type,
      kind: node.kind,
      activityType: node.type === "activity" ? node.config.activityType : undefined,
      title: node.title,
      config: { ...node.config }
    }),
    fromRuntimeDto: (dto, position) => createNodeFromRegistry(entry as MicroflowNodeRegistryEntry, dto.nodeId, position, dto.title)
  };
}

function eventEntry(type: Extract<MicroflowNodeType, "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent">, title: string, titleZh: string, ports: MicroflowPort[], tone: MicroflowRenderMetadata["tone"], description: string): MicroflowNodeRegistryEntry {
  return createEntry({
    type,
    kind: "event",
    title,
    titleZh,
    description,
    category: "events",
    group: "Events",
    iconKey: type,
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: {},
    ports,
    documentation: doc(description),
    render: { iconKey: type, shape: "event", tone, width: 124, height: 70 },
    propertyForm: { formKey: type, sections: type === "endEvent" ? ["General", "Output"] : ["General"] }
  });
}

function activityEntry(item: {
  activityType: MicroflowActivityType;
  title: string;
  titleZh: string;
  activityCategory: MicroflowActivityCategory;
  config?: Partial<MicroflowActivityConfig>;
  description: string;
  availability?: MicroflowNodeAvailability;
  supportsErrorHandling?: boolean;
  supportedErrorHandlingTypes?: MicroflowErrorHandlingType[];
}): MicroflowNodeRegistryEntry<MicroflowActivityConfig> {
  const availability = item.availability ?? "supported";
  const supportsErrorHandling = item.supportsErrorHandling ?? !["variable", "client", "logging", "metrics"].includes(item.activityCategory);
  return createEntry<MicroflowActivityConfig>({
    type: "activity",
    kind: "activity",
    activityType: item.activityType,
    title: item.title,
    titleZh: item.titleZh,
    description: item.description,
    category: "activities",
    activityCategory: item.activityCategory,
    group: "Activities",
    subgroup: item.activityCategory,
    iconKey: item.activityType,
    availability,
    availabilityReason: availabilityReason(availability),
    defaultConfig: {
      activityType: item.activityType,
      activityCategory: item.activityCategory,
      supportsErrorFlow: supportsErrorHandling,
      errorHandling: supportsErrorHandling ? { mode: "rollback", errorVariableName: "latestError" } : undefined,
      ...item.config
    },
    ports: supportsErrorHandling ? [sequenceIn, sequenceOut, errorOut] : [sequenceIn, sequenceOut],
    documentation: doc(item.description),
    supportsErrorHandling,
    supportedErrorHandlingTypes: item.supportedErrorHandlingTypes ?? (supportsErrorHandling ? ["rollback", "customWithRollback", "customWithoutRollback"] : []),
    render: { iconKey: item.activityType, shape: "roundedRect", tone: availability === "deprecated" ? "warning" : availability === "beta" ? "info" : "neutral", width: 178, height: 76 },
    propertyForm: { formKey: `activity:${item.activityType}`, sections: supportsErrorHandling ? ["General", "Output", "Error Handling", "Advanced"] : ["General", "Output"] }
  });
}

const activityDefinitions: Array<Parameters<typeof activityEntry>[0]> = [
  { activityType: "objectCast", title: "Cast Object", titleZh: "转换对象", activityCategory: "object", config: { objectVariableName: "sourceObject", targetEntity: "Sales.Professor", resultVariableName: "professor" }, description: "Casts a generalized object to a specialization entity." },
  { activityType: "objectCreate", title: "Create Object", titleZh: "创建对象", activityCategory: "object", config: { entity: "Sales.Order", objectVariableName: "newOrder" }, description: "Creates an object instance and stores it in a variable." },
  { activityType: "objectChange", title: "Change Object", titleZh: "修改对象", activityCategory: "object", config: { objectVariableName: "order" }, description: "Changes values on an existing object." },
  { activityType: "objectCommit", title: "Commit Object(s)", titleZh: "提交对象", activityCategory: "object", config: { objectVariableName: "order" }, description: "Persists one object or object list and optionally triggers events." },
  { activityType: "objectDelete", title: "Delete Object(s)", titleZh: "删除对象", activityCategory: "object", config: { objectVariableName: "order" }, description: "Deletes one object or object list." },
  { activityType: "objectRetrieve", title: "Retrieve Object(s)", titleZh: "检索对象", activityCategory: "object", config: { entity: "Sales.Order", listVariableName: "orders" }, description: "Retrieves one or more objects from association or database." },
  { activityType: "objectRollback", title: "Rollback Object", titleZh: "回滚对象", activityCategory: "object", config: { objectVariableName: "order" }, description: "Rolls back uncommitted changes on an object or object list." },
  { activityType: "listAggregate", title: "Aggregate List", titleZh: "列表聚合", activityCategory: "list", config: { listVariableName: "orders", operation: "count", resultVariableName: "orderCount" }, description: "Aggregates a list with count, sum, average, min, or max." },
  { activityType: "listCreate", title: "Create List", titleZh: "创建列表", activityCategory: "list", config: { entity: "Sales.Order", listVariableName: "orders" }, description: "Creates an empty typed list variable." },
  { activityType: "listChange", title: "Change List", titleZh: "修改列表", activityCategory: "list", config: { listVariableName: "orders", operation: "add" }, description: "Adds, removes, clears, or replaces list contents." },
  { activityType: "listOperation", title: "List Operation", titleZh: "列表操作", activityCategory: "list", config: { listVariableName: "orders", operation: "filter" }, description: "Combines, compares, filters, sorts, or selects list items." },
  { activityType: "callMicroflow", title: "Call Microflow", titleZh: "调用微流", activityCategory: "call", config: { targetMicroflowId: "MF_ValidateOrder" }, description: "Calls another microflow with parameter mapping.", supportedErrorHandlingTypes: ["rollback", "customWithRollback", "customWithoutRollback", "continue"] },
  { activityType: "callJavaAction", title: "Call Java Action", titleZh: "调用 Java 动作", activityCategory: "call", config: { targetMicroflowId: "JA_Action" }, description: "Calls a server-side Java action." },
  { activityType: "callJavaScriptAction", title: "Call JavaScript Action", titleZh: "调用 JavaScript 动作", activityCategory: "call", config: { targetMicroflowId: "JS_ClientAction" }, description: "Nanoflow-only JavaScript action, disabled in Microflow.", availability: "nanoflowOnlyDisabled" },
  { activityType: "variableCreate", title: "Create Variable", titleZh: "创建变量", activityCategory: "variable", config: { variableName: "result", variableType: { kind: "primitive", name: "String" } }, description: "Creates a local microflow variable.", supportsErrorHandling: false },
  { activityType: "variableChange", title: "Change Variable", titleZh: "修改变量", activityCategory: "variable", config: { variableName: "result" }, description: "Changes the value of an existing variable.", supportsErrorHandling: false },
  { activityType: "closePage", title: "Close Page", titleZh: "关闭页面", activityCategory: "client", config: { closeMode: "current" }, description: "Closes the current or last opened page.", supportsErrorHandling: false },
  { activityType: "downloadFile", title: "Download File", titleZh: "下载文件", activityCategory: "client", config: { objectVariableName: "fileDocument" }, description: "Downloads a FileDocument in the browser.", supportsErrorHandling: false },
  { activityType: "showHomePage", title: "Show Home Page", titleZh: "显示首页", activityCategory: "client", config: {}, description: "Navigates the user to the home page.", supportsErrorHandling: false },
  { activityType: "showMessage", title: "Show Message", titleZh: "显示消息", activityCategory: "client", config: { messageExpression: { id: "expr-message", language: "plainText", text: "", raw: "", referencedVariables: [] } }, description: "Displays a blocking or non-blocking message.", supportsErrorHandling: false },
  { activityType: "showPage", title: "Show Page", titleZh: "显示页面", activityCategory: "client", config: { pageName: "Order.Detail" }, description: "Opens a page for the current user.", supportsErrorHandling: false },
  { activityType: "synchronizeToDevice", title: "Synchronize To Device", titleZh: "同步到设备", activityCategory: "client", config: { objectVariableName: "objectOrList" }, description: "Synchronizes objects to an offline device.", supportsErrorHandling: false },
  { activityType: "validationFeedback", title: "Validation Feedback", titleZh: "验证反馈", activityCategory: "client", config: { objectVariableName: "order", targetMember: "Status" }, description: "Shows validation feedback under a page field.", supportsErrorHandling: false },
  { activityType: "callNanoflow", title: "Call Nanoflow", titleZh: "调用纳流", activityCategory: "client", config: { targetMicroflowId: "NF_ClientAction" }, description: "Nanoflow-only call node, disabled in Microflow.", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false },
  { activityType: "synchronize", title: "Synchronize", titleZh: "同步", activityCategory: "client", config: {}, description: "Nanoflow-only synchronize node, disabled in Microflow.", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false },
  { activityType: "callRest", title: "Call REST Service", titleZh: "调用 REST 服务", activityCategory: "integration", config: { method: "POST", url: "/api/orders/sync" }, description: "Calls a REST endpoint with request and response mapping." },
  { activityType: "callWebService", title: "Call Web Service", titleZh: "调用 Web Service", activityCategory: "integration", config: { serviceId: "", operation: "" }, description: "Calls an imported SOAP/Web Service." },
  { activityType: "callExternalAction", title: "Call External Action", titleZh: "调用外部动作", activityCategory: "integration", config: { serviceId: "", externalActionId: "" }, description: "Calls an OData consumed service external action." },
  { activityType: "importWithMapping", title: "Import With Mapping", titleZh: "使用映射导入", activityCategory: "integration", config: { sourceVariableName: "", mappingId: "" }, description: "Imports XML/JSON through an import mapping." },
  { activityType: "exportWithMapping", title: "Export With Mapping", titleZh: "使用映射导出", activityCategory: "integration", config: { sourceVariableName: "", mappingId: "", outputType: "string" }, description: "Exports objects through an export mapping." },
  { activityType: "queryExternalDatabase", title: "Query External Database", titleZh: "查询外部数据库", activityCategory: "integration", config: { connectorId: "", operation: "query" }, description: "Queries an external database connector.", availability: "requiresConnector" },
  { activityType: "sendRestRequestBeta", title: "Send REST Request", titleZh: "发送 REST 请求", activityCategory: "integration", config: { serviceId: "", operation: "" }, description: "Sends a request from a consumed REST service document.", availability: "beta" },
  { activityType: "logMessage", title: "Log Message", titleZh: "记录日志", activityCategory: "logging", config: { logLevel: "info", messageExpression: { id: "expr-log", language: "plainText", text: "Order processed", raw: "Order processed", referencedVariables: [] } }, description: "Writes an application log entry.", supportsErrorHandling: false },
  { activityType: "generateDocument", title: "Generate Document", titleZh: "生成文档", activityCategory: "documentGeneration", config: { mappingId: "", objectVariableName: "fileDocument" }, description: "Generates a document from a template for legacy compatibility.", availability: "deprecated" },
  { activityType: "counter", title: "Counter", titleZh: "计数器", activityCategory: "metrics", config: { metricName: "", valueExpression: { id: "expr-counter", language: "mendix", text: "1", raw: "1", referencedVariables: [] } }, description: "Sets or increases a custom counter metric.", supportsErrorHandling: false },
  { activityType: "incrementCounter", title: "Increment Counter", titleZh: "计数器加一", activityCategory: "metrics", config: { metricName: "" }, description: "Increments a counter metric by one.", supportsErrorHandling: false },
  { activityType: "gauge", title: "Gauge", titleZh: "仪表指标", activityCategory: "metrics", config: { metricName: "", valueExpression: { id: "expr-gauge", language: "mendix", text: "0", raw: "0", referencedVariables: [] } }, description: "Sets a gauge metric value.", supportsErrorHandling: false },
  { activityType: "callMlModel", title: "Call ML Model", titleZh: "调用 ML 模型", activityCategory: "mlKit", config: { mappingId: "" }, description: "Calls an ML model mapping." },
  { activityType: "applyJumpToOption", title: "Apply Jump-To Option", titleZh: "应用跳转选项", activityCategory: "workflow", config: { workflowInstanceVariable: "", objectVariableName: "jumpToOption" }, description: "Applies a generated workflow jump-to option." },
  { activityType: "callWorkflow", title: "Call Workflow", titleZh: "调用工作流", activityCategory: "workflow", config: { targetMicroflowId: "WF_Order", objectVariableName: "contextObject" }, description: "Starts a target workflow." },
  { activityType: "changeWorkflowState", title: "Change Workflow State", titleZh: "修改工作流状态", activityCategory: "workflow", config: { workflowInstanceVariable: "", operation: "pause" }, description: "Changes workflow state such as abort, continue, pause, retry." },
  { activityType: "completeUserTask", title: "Complete User Task", titleZh: "完成用户任务", activityCategory: "workflow", config: { objectVariableName: "userTask", operation: "outcome" }, description: "Completes a user task with an outcome." },
  { activityType: "generateJumpToOptions", title: "Generate Jump-To Options", titleZh: "生成跳转选项", activityCategory: "workflow", config: { workflowInstanceVariable: "", resultVariableName: "jumpToOptions" }, description: "Generates workflow jump-to options." },
  { activityType: "retrieveWorkflowActivityRecords", title: "Retrieve Workflow Activity Records", titleZh: "检索工作流活动记录", activityCategory: "workflow", config: { workflowInstanceVariable: "", listVariableName: "activityRecords" }, description: "Retrieves workflow activity records." },
  { activityType: "retrieveWorkflowContext", title: "Retrieve Workflow Context", titleZh: "检索工作流上下文", activityCategory: "workflow", config: { workflowInstanceVariable: "", entity: "Workflow.Context", objectVariableName: "context" }, description: "Retrieves workflow context object." },
  { activityType: "retrieveWorkflows", title: "Retrieve Workflows", titleZh: "检索工作流", activityCategory: "workflow", config: { objectVariableName: "contextObject", listVariableName: "workflows" }, description: "Retrieves workflows by context." },
  { activityType: "showUserTaskPage", title: "Show User Task Page", titleZh: "显示用户任务页面", activityCategory: "workflow", config: { objectVariableName: "userTask" }, description: "Opens the configured user task page.", supportsErrorHandling: false },
  { activityType: "showWorkflowAdminPage", title: "Show Workflow Admin Page", titleZh: "显示工作流管理页", activityCategory: "workflow", config: { workflowInstanceVariable: "" }, description: "Opens the workflow admin page.", supportsErrorHandling: false },
  { activityType: "lockWorkflow", title: "Lock Workflow", titleZh: "锁定工作流", activityCategory: "workflow", config: { workflowInstanceVariable: "" }, description: "Locks a workflow instance.", supportsErrorHandling: false },
  { activityType: "unlockWorkflow", title: "Unlock Workflow", titleZh: "解锁工作流", activityCategory: "workflow", config: { workflowInstanceVariable: "" }, description: "Unlocks a workflow instance.", supportsErrorHandling: false },
  { activityType: "notifyWorkflow", title: "Notify Workflow", titleZh: "通知工作流", activityCategory: "workflow", config: { workflowInstanceVariable: "", operation: "notification" }, description: "Notifies a waiting workflow instance.", supportsErrorHandling: false },
  { activityType: "deleteExternalObject", title: "Delete External Object", titleZh: "删除外部对象", activityCategory: "externalObject", config: { objectVariableName: "externalObject", operation: "delete" }, description: "Deletes an external object via service operation." },
  { activityType: "sendExternalObject", title: "Send External Object", titleZh: "发送外部对象", activityCategory: "externalObject", config: { objectVariableName: "externalObject", operation: "send" }, description: "Sends or updates an external object through a service operation." }
];

export const microflowObjectNodeRegistries: MicroflowNodeRegistryEntry[] = [
  eventEntry("startEvent", "Start Event", "开始事件", [sequenceOut], "success", "Unique entry point of a microflow."),
  eventEntry("endEvent", "End Event", "结束事件", [sequenceIn], "danger", "Stops the microflow and optionally returns a value."),
  eventEntry("errorEvent", "Error Event", "错误事件", [sequenceIn], "danger", "Stops and throws an error from an error handling scope."),
  eventEntry("continueEvent", "Continue Event", "继续事件", [sequenceIn], "warning", "Continues with the next loop iteration."),
  eventEntry("breakEvent", "Break Event", "中断事件", [sequenceIn], "warning", "Breaks out of the nearest loop."),
  createEntry({
    type: "decision",
    kind: "decision",
    title: "Decision",
    titleZh: "决策",
    description: "Branches the microflow with a boolean or enumeration outcome.",
    category: "decisions",
    group: "Decisions",
    iconKey: "decision",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { expression: { id: "expr-decision", language: "mendix", text: "", raw: "", referencedVariables: [] }, resultType: "Boolean" },
    ports: [sequenceIn, decisionTrue, decisionFalse, errorOut],
    documentation: doc("Evaluates an expression or rule and routes execution to exactly one branch."),
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 152, height: 104 },
    propertyForm: { formKey: "decision", sections: ["General", "Output", "Error Handling"] }
  }),
  createEntry({
    type: "objectTypeDecision",
    kind: "objectTypeDecision",
    title: "Object Type Decision",
    titleZh: "对象类型决策",
    description: "Branches by runtime specialization entity, empty, or fallback.",
    category: "decisions",
    group: "Decisions",
    iconKey: "objectTypeDecision",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { inputObject: "", generalizedEntity: "" },
    ports: [sequenceIn, objectTypeOut, errorOut],
    documentation: doc("Selects a branch according to an object's actual specialization entity."),
    render: { iconKey: "objectTypeDecision", shape: "diamond", tone: "warning", width: 168, height: 112 },
    propertyForm: { formKey: "objectTypeDecision", sections: ["General", "Output", "Error Handling"] }
  }),
  createEntry({
    type: "merge",
    kind: "merge",
    title: "Merge",
    titleZh: "合并",
    description: "Combines mutually exclusive branches into one continuation.",
    category: "decisions",
    group: "Decisions",
    iconKey: "merge",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { strategy: "firstAvailable" },
    ports: [sequenceIn, sequenceOut],
    documentation: doc("Merge is not a parallel synchronizer; it continues when any incoming branch arrives."),
    render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 },
    propertyForm: { formKey: "merge", sections: ["General", "Output"] },
    supportsErrorHandling: false
  }),
  createEntry({
    type: "loop",
    kind: "loop",
    title: "Loop",
    titleZh: "循环",
    description: "For-each or while loop container.",
    category: "loop",
    group: "Loop",
    iconKey: "loop",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { iterableVariableName: "", itemVariableName: "currentItem", loopType: "forEach", indexVariableName: "currentIndex" },
    ports: [sequenceIn, sequenceOut, port("bodyIn", "Body In", "output", "loopBodyIn", "one", ["sequence"]), port("bodyOut", "Body Out", "input", "loopBodyOut", "zeroOrMore", ["sequence"]), errorOut],
    documentation: doc("Runs child microflow elements for every list item or while an expression is true."),
    supportedErrorHandlingTypes: ["rollback", "customWithRollback", "customWithoutRollback", "continue"],
    render: { iconKey: "loop", shape: "loop", tone: "info", width: 178, height: 82 },
    propertyForm: { formKey: "loop", sections: ["General", "Output", "Error Handling"] }
  }),
  createEntry({
    type: "parameter",
    kind: "parameter",
    title: "Parameter",
    titleZh: "输入参数",
    description: "Defines an input parameter for the microflow.",
    category: "parameters",
    group: "Parameters",
    iconKey: "parameter",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { parameter: { id: "parameter", name: "input", required: true, type: { kind: "unknown", name: "Unknown" } } },
    ports: [annotationOut],
    documentation: doc("Input data supplied by callers and referenced by expressions or activities."),
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 158, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["General"] },
    supportsErrorHandling: false
  }),
  createEntry({
    type: "annotation",
    kind: "annotation",
    title: "Annotation",
    titleZh: "注释",
    description: "Adds documentation to the canvas without affecting execution.",
    category: "annotations",
    group: "Annotations",
    iconKey: "annotation",
    availability: "supported",
    availabilityReason: undefined,
    defaultConfig: { text: "Describe the intent of this part of the microflow." },
    ports: [annotationOut],
    documentation: doc("Canvas-only documentation connected through annotation flows."),
    render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 220, height: 100 },
    propertyForm: { formKey: "annotation", sections: ["General"] },
    supportsErrorHandling: false
  })
];

function nodePanelEntryFromActionRegistry(actionItem: MicroflowActionRegistryItem): MicroflowNodeRegistryEntry<MicroflowActivityConfig> {
  const activityItem = activityEntry({
    activityType: actionItem.legacyActivityType,
    title: actionItem.title,
    titleZh: actionItem.titleZh,
    activityCategory: actionItem.category,
    config: actionItem.defaultConfig,
    description: actionItem.description,
    availability: actionItem.availability === "hidden" ? "supported" : actionItem.availability,
    supportsErrorHandling: actionItem.supportsErrorHandling,
    supportedErrorHandlingTypes: actionItem.supportedErrorHandlingTypes
  });
  return {
    ...activityItem,
    key: `activity:${actionItem.legacyActivityType}`,
    actionKind: actionItem.actionKind,
    officialType: "Microflows$ActionActivity",
    defaultCaption: actionItem.defaultCaption,
    propertyTabs: actionItem.propertyTabs,
    toRuntimeDto: () => ({
      nodeId: actionItem.key,
      type: "activity",
      kind: "activity",
      activityType: actionItem.legacyActivityType,
      title: actionItem.title,
      config: {
        kind: "nodePanelAction",
        actionKind: actionItem.actionKind,
        officialType: actionItem.officialType,
        unsupported: true
      }
    })
  };
}

export const microflowActionNodePanelRegistries: MicroflowNodeRegistryEntry[] = defaultMicroflowActionRegistry
  .filter(item => item.availability !== "hidden")
  .map(nodePanelEntryFromActionRegistry);

export const microflowNodeRegistries: MicroflowNodeRegistryEntry[] = microflowObjectNodeRegistries;
export const microflowNodePanelRegistries: MicroflowNodeRegistryEntry[] = [
  ...microflowObjectNodeRegistries.filter(item => item.type !== "activity"),
  ...microflowActionNodePanelRegistries
];

export const microflowNodeRegistryByKey = new Map<string, MicroflowNodeRegistryEntry>(
  microflowNodePanelRegistries.flatMap(entry => {
    const keys = [getMicroflowNodeRegistryKey(entry)];
    if (entry.activityType) {
      keys.push(`${entry.type}:${entry.activityType}`);
    }
    return keys.map(key => [key, entry] as const);
  })
);

export const defaultMicroflowObjectNodeRegistry = microflowObjectNodeRegistries;
export const defaultMicroflowNodeRegistry = microflowNodePanelRegistries;
export const defaultMicroflowNodePanelRegistry = microflowNodePanelRegistries;

export function getMicroflowNodeRegistryKey(entry: Pick<MicroflowNodeRegistryEntry, "type" | "activityType">): string {
  if ("key" in entry && typeof entry.key === "string") {
    return entry.key;
  }
  return entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type;
}

export function objectKindFromRegistryItem(entry: Pick<MicroflowNodeRegistryEntry, "type">): MicroflowObjectKind {
  const map: Record<MicroflowNodeType, MicroflowObjectKind> = {
    startEvent: "startEvent",
    endEvent: "endEvent",
    errorEvent: "errorEvent",
    breakEvent: "breakEvent",
    continueEvent: "continueEvent",
    decision: "exclusiveSplit",
    objectTypeDecision: "inheritanceSplit",
    merge: "exclusiveMerge",
    loop: "loopedActivity",
    parameter: "parameterObject",
    annotation: "annotation",
    activity: "actionActivity"
  };
  return map[entry.type];
}

export function officialTypeFromRegistryItem(entry: Pick<MicroflowNodeRegistryEntry, "type">): string {
  const map: Record<MicroflowNodeType, string> = {
    startEvent: "Microflows$StartEvent",
    endEvent: "Microflows$EndEvent",
    errorEvent: "Microflows$ErrorEvent",
    breakEvent: "Microflows$BreakEvent",
    continueEvent: "Microflows$ContinueEvent",
    decision: "Microflows$ExclusiveSplit",
    objectTypeDecision: "Microflows$InheritanceSplit",
    merge: "Microflows$ExclusiveMerge",
    loop: "Microflows$LoopedActivity",
    parameter: "Microflows$MicroflowParameterObject",
    annotation: "Microflows$Annotation",
    activity: "Microflows$ActionActivity"
  };
  return map[entry.type];
}

export function actionKindFromActivityType(activityType?: MicroflowActivityType): MicroflowActionKind | undefined {
  if (!activityType) {
    return undefined;
  }
  return microflowActionRegistryByActivityType.get(activityType)?.kind;
}

export function getDisabledDragReason(entry: MicroflowNodeRegistryEntry): string | undefined {
  if (!entry.enabled) {
    return entry.disabledReason ?? entry.availabilityReason ?? "Node is disabled.";
  }
  if (entry.availability === "nanoflowOnlyDisabled") {
    return entry.availabilityReason ?? "Nanoflow-only node cannot be used in Microflow.";
  }
  if (entry.availability === "requiresConnector") {
    return entry.availabilityReason ?? "Connector is required before this node can be used.";
  }
  return undefined;
}

export function canDragRegistryItem(entry: MicroflowNodeRegistryEntry): boolean {
  return !getDisabledDragReason(entry);
}

export function createDragPayloadFromRegistryItem(entry: MicroflowNodeRegistryEntry): MicroflowNodeDragPayload {
  return {
    dragType: "microflow-node",
    nodeType: entry.type,
    objectKind: objectKindFromRegistryItem(entry),
    activityType: entry.activityType,
    actionKind: actionKindFromActivityType(entry.activityType),
    registryKey: getMicroflowNodeRegistryKey(entry),
    title: entry.title,
    defaultConfig: JSON.parse(JSON.stringify(entry.defaultConfig)) as Record<string, unknown>,
    sourcePanel: "nodes"
  };
}

function categoryKeyForEntry(entry: MicroflowNodeRegistryEntry): MicroflowNodePanelCategoryKey {
  if (entry.group === "Events") {
    return "events";
  }
  if (entry.group === "Decisions") {
    return "decisions";
  }
  if (entry.group === "Activities") {
    return "activities";
  }
  if (entry.group === "Loop") {
    return "loop";
  }
  if (entry.group === "Parameters") {
    return "parameters";
  }
  return "annotations";
}

export function getMicroflowNodeCategories(registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodePanelRegistry): MicroflowNodeCategoryDefinition[] {
  const available = new Set(registry.map(categoryKeyForEntry));
  return microflowNodeCategoryDefinitions.filter(category => available.has(category.key));
}

export function searchMicroflowNodes(keyword: string, registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodePanelRegistry): MicroflowNodeRegistryEntry[] {
  const normalized = keyword.trim().toLowerCase();
  if (!normalized) {
    return registry;
  }
  return registry.filter(entry => [
    entry.title,
    entry.titleZh,
    entry.description,
    entry.group,
    entry.subgroup,
    entry.iconKey,
    entry.documentation.summary,
    entry.documentation.whenToUse,
    entry.availability,
    entry.availabilityReason,
    ...entry.keywords
  ].filter((value): value is string => Boolean(value)).join(" ").toLowerCase().includes(normalized));
}

export function groupMicroflowNodesByCategory(registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodePanelRegistry): Array<{
  category: MicroflowNodeCategoryDefinition;
  entries: MicroflowNodeRegistryEntry[];
  groups: Array<{ key: MicroflowNodeGroup; label: string; entries: MicroflowNodeRegistryEntry[] }>;
}> {
  return getMicroflowNodeCategories(registry).map(category => {
    const entries = registry.filter(entry => categoryKeyForEntry(entry) === category.key);
    const groups = (category.groups ?? []).map(group => ({
      ...group,
      entries: entries.filter(entry => entry.subgroup === group.key)
    })).filter(group => group.entries.length > 0);
    return { category, entries, groups };
  }).filter(category => category.entries.length > 0);
}

export function getMicroflowNodeByType(
  type: MicroflowNodeType,
  activityType?: MicroflowActivityType,
  registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodePanelRegistry
): MicroflowNodeRegistryEntry | undefined {
  return registry.find(entry => entry.type === type && entry.activityType === activityType);
}

export function createMicroflowNodeFromRegistry(
  registryKey: string,
  id: string,
  position: MicroflowPosition
): MicroflowNode;
export function createMicroflowNodeFromRegistry(
  entry: MicroflowNodeRegistryEntry,
  position: MicroflowPosition,
  id?: string
): MicroflowNode;
export function createMicroflowNodeFromRegistry(
  registryKeyOrEntry: string | MicroflowNodeRegistryEntry,
  idOrPosition: string | MicroflowPosition,
  maybePosition?: MicroflowPosition | string
): MicroflowNode {
  if (typeof registryKeyOrEntry !== "string") {
    const id = typeof maybePosition === "string" ? maybePosition : `${getMicroflowNodeRegistryKey(registryKeyOrEntry).replace(":", "-")}-${Date.now()}`;
    return createNodeFromRegistry(registryKeyOrEntry, id, idOrPosition as MicroflowPosition);
  }
  const registryKey = registryKeyOrEntry;
  const id = idOrPosition as string;
  const position = maybePosition as MicroflowPosition;
  const entry = microflowNodeRegistryByKey.get(registryKey);
  if (!entry) {
    throw new Error(`Unknown microflow registry key: ${registryKey}`);
  }
  return createNodeFromRegistry(entry, id, position);
}
