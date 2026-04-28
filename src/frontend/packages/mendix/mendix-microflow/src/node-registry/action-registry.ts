import type {
  MicroflowAction,
  MicroflowActionCategory,
  MicroflowActionKind,
  LegacyMicroflowActivityConfig,
  MicroflowActivityType,
  MicroflowDataType,
  MicroflowErrorHandlingType,
  MicroflowExpression,
  MicroflowNodeAvailability,
  MicroflowPropertyTabKey,
  MicroflowRuntimeNodeDto,
  MicroflowValidationIssue
} from "../schema/types";
import { createDefaultActionConfig } from "./default-node-config";

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
  defaultConfig: LegacyMicroflowActivityConfig;
  createDefaultConfig: () => LegacyMicroflowActivityConfig;
  outputSpec?: Array<{ id: string; name: string; dataType: MicroflowDataType; source: string }>;
  inputSpec?: Array<{ id: string; title: string; dataType?: MicroflowDataType; required?: boolean }>;
  supportsErrorHandling: boolean;
  supportedErrorHandlingTypes: MicroflowErrorHandlingType[];
  propertyTabs: MicroflowPropertyTabKey[];
  /** 与 {@link resolveActionRuntimeSupportLevel} 对齐的注册表元数据。P0 恒为 supported。 */
  runtimeSupportLevel: MicroflowActionRuntimeSupportLevel;
  createsActionActivity: true;
  createAction: (input: { id: string; config?: Partial<LegacyMicroflowActivityConfig>; caption?: string }) => MicroflowAction;
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

type GenericActionConfig = Record<string, unknown>;

const unknownType: MicroflowDataType = { kind: "unknown", reason: "registry default" };

const P0_ACTION_KINDS = new Set<MicroflowActionKind>([
  "retrieve",
  "createObject",
  "changeMembers",
  "commit",
  "delete",
  "rollback",
  "createVariable",
  "changeVariable",
  "callMicroflow",
  "restCall",
  "logMessage"
]);

export type MicroflowActionRuntimeSupportLevel =
  | "supported"
  | "modeledOnly"
  | "unsupported"
  | "requiresConnector"
  | "nanoflowOnly"
  | "deprecated";
const stringType: MicroflowDataType = { kind: "string" };
const integerType: MicroflowDataType = { kind: "integer" };
const booleanType: MicroflowDataType = { kind: "boolean" };

function cloneRecord(input: GenericActionConfig): GenericActionConfig {
  return JSON.parse(JSON.stringify(input)) as GenericActionConfig;
}

const genericActionDefaults: Partial<Record<MicroflowActionKind, GenericActionConfig>> = {
  cast: {
    sourceObjectVariableName: "",
    targetEntityQualifiedName: "",
    outputVariableName: ""
  },
  aggregateList: {
    listVariableName: "",
    sourceListVariableName: "",
    aggregateFunction: "count",
    attributeQualifiedName: "",
    outputVariableName: "",
    resultVariableName: "",
    resultType: integerType
  },
  createList: {
    entityQualifiedName: "",
    outputListVariableName: "",
    listVariableName: "",
    elementType: stringType,
    itemType: stringType,
    listType: "mutable",
    initialItemsExpression: expression("", { kind: "list", itemType: stringType }),
    description: ""
  },
  changeList: {
    targetListVariableName: "",
    operation: "add",
    objectVariableName: "",
    itemExpression: expression(""),
    itemsExpression: expression("", { kind: "list", itemType: unknownType }),
    conditionExpression: expression("", booleanType),
    indexExpression: expression("", integerType)
  },
  listOperation: {
    leftListVariableName: "",
    sourceListVariableName: "",
    operation: "filter",
    rightListVariableName: "",
    objectVariableName: "",
    expression: expression("", booleanType),
    filterExpression: expression("", booleanType),
    sortExpression: expression(""),
    outputVariableName: "",
    outputListVariableName: "",
    outputElementType: stringType
  },
  callJavaAction: {
    javaActionQualifiedName: "",
    parameterMappings: [],
    returnValue: { storeResult: false, outputVariableName: "" }
  },
  callJavaScriptAction: {
    javaScriptActionQualifiedName: "",
    parameterMappings: [],
    returnValue: { storeResult: false, outputVariableName: "" }
  },
  callNanoflow: {
    targetNanoflowId: "",
    parameterMappings: [],
    returnValue: { storeResult: false, outputVariableName: "" }
  },
  closePage: {
    closeTarget: "current",
    returnResult: ""
  },
  downloadFile: {
    fileDocumentVariableName: "",
    showFileInBrowser: true
  },
  showHomePage: {
    pageTarget: ""
  },
  showMessage: {
    messageType: "information",
    blocking: false,
    messageExpression: expression("", stringType)
  },
  showPage: {
    pageId: "",
    pageParameterMappings: [],
    openMode: "current",
    title: ""
  },
  validationFeedback: {
    targetObjectVariableName: "",
    targetMemberQualifiedName: "",
    feedbackMessage: ""
  },
  synchronize: {
    scope: "all"
  },
  webServiceCall: {
    webServiceQualifiedName: "",
    operationName: "",
    requestMapping: "",
    responseMapping: "",
    outputVariableName: ""
  },
  importXml: {
    sourceType: "string",
    sourceVariableName: "",
    importMappingQualifiedName: "",
    outputVariableName: ""
  },
  exportXml: {
    sourceVariableName: "",
    exportMappingQualifiedName: "",
    outputType: "string",
    outputVariableName: ""
  },
  callExternalAction: {
    consumedServiceQualifiedName: "",
    externalActionName: "",
    parameterMappings: [],
    returnVariableName: ""
  },
  restOperationCall: {
    consumedRestServiceQualifiedName: "",
    operationName: "",
    parameterMappings: [],
    outputVariableName: ""
  },
  generateDocument: {
    documentTemplateQualifiedName: "",
    outputFileDocumentVariableName: "",
    parameterMappings: []
  },
  counter: {
    metricName: "",
    valueExpression: expression("1", integerType),
    tags: []
  },
  incrementCounter: {
    metricName: "",
    tags: []
  },
  gauge: {
    metricName: "",
    valueExpression: expression("0", integerType),
    tags: []
  },
  mlModelCall: {
    modelMappingQualifiedName: "",
    inputMappings: [],
    outputVariableName: ""
  },
  applyJumpToOption: {
    workflowInstanceVariableName: "",
    targetVariableName: "",
    outputVariableName: "",
  },
  callWorkflow: {
    targetWorkflowId: "",
    contextObjectVariableName: "",
    outputWorkflowVariableName: ""
  },
  changeWorkflowState: {
    workflowInstanceVariableName: "",
    operation: "pause",
    reason: ""
  },
  completeUserTask: {
    userTaskVariableName: "",
    outcome: "",
    validationResult: ""
  },
  generateJumpToOptions: {
    workflowInstanceVariableName: "",
    targetVariableName: "",
    outputVariableName: ""
  },
  retrieveWorkflowActivityRecords: {
    workflowInstanceVariableName: "",
    targetVariableName: "",
    outputVariableName: ""
  },
  retrieveWorkflowContext: {
    workflowInstanceVariableName: "",
    contextEntityQualifiedName: "",
    outputVariableName: "workflowContext"
  },
  retrieveWorkflows: {
    contextObjectVariableName: "",
    outputListVariableName: "",
    filters: []
  },
  showUserTaskPage: {
    userTaskVariableName: "",
    targetVariableName: "userTask"
  },
  showWorkflowAdminPage: {
    workflowInstanceVariableName: "",
    targetVariableName: "workflowInstance"
  },
  lockWorkflow: {
    workflowInstanceVariableName: "",
    targetVariableName: "workflowInstance"
  },
  unlockWorkflow: {
    workflowInstanceVariableName: "",
    targetVariableName: "workflowInstance"
  },
  notifyWorkflow: {
    workflowInstanceVariableName: "",
    notificationName: "",
    payloadExpression: expression("", unknownType)
  },
  deleteExternalObject: {
    externalObjectVariableName: "",
    serviceOperationName: ""
  },
  sendExternalObject: {
    externalObjectVariableName: "",
    serviceOperationName: "",
    payloadMapping: ""
  }
};

const actionOutputs: Partial<Record<MicroflowActionKind, MicroflowActionRegistryItem["outputSpec"]>> = {
  cast: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "cast" }],
  createObject: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "createObject" }],
  retrieve: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "retrieve" }],
  aggregateList: [{ id: "output", name: "outputVariableName", dataType: integerType, source: "aggregateList" }],
  createList: [{ id: "output", name: "outputListVariableName", dataType: { kind: "list", itemType: unknownType }, source: "createList" }],
  listOperation: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "listOperation" }],
  callMicroflow: [{ id: "return", name: "returnValue.outputVariableName", dataType: unknownType, source: "callMicroflow" }],
  createVariable: [{ id: "variable", name: "variableName", dataType: unknownType, source: "createVariable" }],
  restCall: [{ id: "response", name: "response.handling.outputVariableName", dataType: unknownType, source: "restCall" }],
  webServiceCall: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "webServiceCall" }],
  importXml: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "importXml" }],
  exportXml: [{ id: "output", name: "outputVariableName", dataType: stringType, source: "exportXml" }],
  callExternalAction: [{ id: "return", name: "returnVariableName", dataType: unknownType, source: "callExternalAction" }],
  restOperationCall: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "restOperationCall" }],
  mlModelCall: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "mlModelCall" }],
  callWorkflow: [{ id: "workflow", name: "outputWorkflowVariableName", dataType: { kind: "object", entityQualifiedName: "Workflow.Workflow" }, source: "callWorkflow" }],
  generateJumpToOptions: [{ id: "output", name: "outputVariableName", dataType: { kind: "list", itemType: unknownType }, source: "generateJumpToOptions" }],
  retrieveWorkflowActivityRecords: [{ id: "output", name: "outputVariableName", dataType: { kind: "list", itemType: unknownType }, source: "retrieveWorkflowActivityRecords" }],
  retrieveWorkflowContext: [{ id: "output", name: "outputVariableName", dataType: unknownType, source: "retrieveWorkflowContext" }],
  retrieveWorkflows: [{ id: "output", name: "outputListVariableName", dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Workflow.Workflow" } }, source: "retrieveWorkflows" }]
};

function createConcreteAction(item: MicroflowActionRegistryItem, id: string, config: Partial<LegacyMicroflowActivityConfig> = {}, caption?: string): MicroflowAction {
  const base = baseAction(item, id, caption);
  if (item.actionKind === "retrieve") {
    return {
      ...base,
      kind: "retrieve",
      officialType: "Microflows$RetrieveAction",
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? ""),
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
      entityQualifiedName: String(config.entity ?? ""),
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? ""),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false }
    };
  }
  if (item.actionKind === "changeMembers") {
    return {
      ...base,
      kind: "changeMembers",
      officialType: "Microflows$ChangeMembersAction",
      changeVariableName: String(config.objectVariableName ?? ""),
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
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      withEvents: true,
      refreshInClient: false
    };
  }
  if (item.actionKind === "delete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      withEvents: true,
      deleteBehavior: "deleteOnly"
    };
  }
  if (item.actionKind === "rollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      refreshInClient: false
    };
  }
  if (item.actionKind === "callMicroflow") {
    return {
      ...base,
      kind: "callMicroflow",
      officialType: "Microflows$MicroflowCallAction",
      targetMicroflowId: String(config.targetMicroflowId ?? ""),
      targetMicroflowName: "",
      targetMicroflowQualifiedName: String(config.targetMicroflowQualifiedName ?? ""),
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
      variableName: String(config.variableName ?? "newVariable"),
      dataType: { kind: "string" },
      initialValue: expression("", { kind: "string" }),
      readonly: false
    };
  }
  if (item.actionKind === "changeVariable") {
    return {
      ...base,
      kind: "changeVariable",
      officialType: "Microflows$ChangeVariableAction",
      targetVariableName: String(config.variableName ?? ""),
      newValueExpression: expression("")
    };
  }
  if (item.actionKind === "createList") {
    const elementType = typeof config.elementType === "object" && config.elementType
      ? config.elementType as MicroflowDataType
      : stringType;
    const outputListVariableName = String(config.listVariableName ?? config.outputListVariableName ?? "");
    return {
      ...base,
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      outputListVariableName,
      listVariableName: outputListVariableName,
      listVariableId: id,
      elementType,
      itemType: elementType,
      listType: "mutable",
      entityQualifiedName: String(config.entity ?? config.entityQualifiedName ?? ""),
      initialItemsExpression: expression("", { kind: "list", itemType: elementType }),
      description: ""
    };
  }
  if (item.actionKind === "changeList") {
    return {
      ...base,
      kind: "changeList",
      officialType: "Microflows$ChangeListAction",
      targetListVariableName: String(config.listVariableName ?? config.targetListVariableName ?? ""),
      operation: String(config.operation ?? "add") as "add",
      itemExpression: expression(""),
      itemsExpression: expression("", { kind: "list", itemType: unknownType }),
      conditionExpression: expression("", booleanType),
      indexExpression: expression("", integerType)
    };
  }
  if (item.actionKind === "aggregateList") {
    return {
      ...base,
      kind: "aggregateList",
      officialType: "Microflows$AggregateListAction",
      listVariableName: String(config.listVariableName ?? config.sourceListVariableName ?? ""),
      sourceListVariableName: String(config.listVariableName ?? config.sourceListVariableName ?? ""),
      aggregateFunction: String(config.operation ?? config.aggregateFunction ?? "count") as "count",
      attributeQualifiedName: "",
      aggregateExpression: expression(""),
      outputVariableName: String(config.resultVariableName ?? config.outputVariableName ?? ""),
      resultVariableName: String(config.resultVariableName ?? config.outputVariableName ?? ""),
      resultType: integerType
    };
  }
  if (item.actionKind === "listOperation") {
    const sourceListVariableName = String(config.listVariableName ?? config.leftListVariableName ?? config.sourceListVariableName ?? "");
    const outputVariableName = String(config.resultVariableName ?? config.outputVariableName ?? config.outputListVariableName ?? "");
    return {
      ...base,
      kind: "listOperation",
      officialType: "Microflows$ListOperationAction",
      leftListVariableName: sourceListVariableName,
      sourceListVariableName,
      operation: String(config.operation ?? "filter") as "filter",
      rightListVariableName: "",
      objectVariableName: "",
      expression: expression("", booleanType),
      filterExpression: expression("", booleanType),
      sortExpression: expression(""),
      outputVariableName,
      outputListVariableName: outputVariableName,
      outputElementType: stringType
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
      template: { text: "", arguments: [] },
      includeContextVariables: false,
      includeTraceId: true
    };
  }
  return {
    ...base,
    kind: item.actionKind,
    officialType: item.officialType,
    ...cloneRecord((genericActionDefaults[item.actionKind] ?? {}) as GenericActionConfig),
    legacyActivityType: item.legacyActivityType,
    legacyConfig: config
  } as MicroflowAction;
}

function validateAction(action: MicroflowAction): MicroflowValidationIssue[] {
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
  if (action.kind === "createList" && !action.outputListVariableName.trim()) {
    return [issue("MF_ACTION_CREATE_LIST_NAME_MISSING", "Create List action requires a list variable name.", action.id, "warning")];
  }
  if (action.kind === "changeList" && !action.targetListVariableName.trim()) {
    return [issue("MF_ACTION_CHANGE_LIST_TARGET_MISSING", "Change List action requires a target list variable.", action.id, "warning")];
  }
  if (action.kind === "aggregateList" && !action.listVariableName.trim()) {
    return [issue("MF_ACTION_AGGREGATE_LIST_SOURCE_MISSING", "Aggregate List action requires a source list variable.", action.id, "warning")];
  }
  if (action.kind === "aggregateList" && !action.outputVariableName.trim()) {
    return [issue("MF_ACTION_AGGREGATE_LIST_RESULT_MISSING", "Aggregate List action requires a result variable.", action.id, "warning")];
  }
  if (action.kind === "listOperation" && !action.leftListVariableName.trim()) {
    return [issue("MF_ACTION_LIST_OPERATION_SOURCE_MISSING", "List Operation action requires a source list variable.", action.id, "warning")];
  }
  if (action.kind === "listOperation" && !action.outputVariableName.trim()) {
    return [issue("MF_ACTION_LIST_OPERATION_OUTPUT_MISSING", "List Operation action requires an output list variable.", action.id, "warning")];
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
  defaultConfig?: Partial<LegacyMicroflowActivityConfig>;
  outputSpec?: MicroflowActionRegistryItem["outputSpec"];
  inputSpec?: MicroflowActionRegistryItem["inputSpec"];
  supportsErrorHandling?: boolean;
}): MicroflowActionRegistryItem {
  const availability = input.availability ?? "supported";
  const supportsErrorHandling = input.supportsErrorHandling ?? !["client", "logging", "metrics", "variable"].includes(input.category);
  const runtimeSupportLevel: MicroflowActionRuntimeSupportLevel = P0_ACTION_KINDS.has(input.key)
    ? "supported"
    : availability === "hidden"
      ? "modeledOnly"
      : availability === "nanoflowOnlyDisabled"
        ? "nanoflowOnly"
        : availability === "requiresConnector"
          ? "requiresConnector"
          : availability === "deprecated"
            ? "deprecated"
            : availability === "beta"
              ? "modeledOnly"
              : "modeledOnly";
  const createDefaultConfig = () => ({
    activityType: input.legacyActivityType,
    activityCategory: input.category,
    supportsErrorFlow: supportsErrorHandling,
    errorHandling: supportsErrorHandling ? { mode: "rollback" as const, errorVariableName: "latestError" } : undefined,
    ...cloneRecord((input.defaultConfig ?? {}) as GenericActionConfig)
  }) as LegacyMicroflowActivityConfig;
  const item: MicroflowActionRegistryItem = {
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
    defaultConfig: createDefaultConfig(),
    createDefaultConfig,
    outputSpec: input.outputSpec ?? actionOutputs[input.key],
    inputSpec: input.inputSpec,
    supportsErrorHandling,
    supportedErrorHandlingTypes: supportsErrorHandling ? ["rollback", "customWithRollback", "customWithoutRollback"] : [],
    propertyTabs: supportsErrorHandling ? ["properties", "documentation", "errorHandling", "output", "advanced"] : ["properties", "documentation", "output"],
    runtimeSupportLevel,
    createsActionActivity: true,
    createAction: ({ id, config, caption }: { id: string; config?: Partial<LegacyMicroflowActivityConfig>; caption?: string }) => createConcreteAction(item, id, { ...item.defaultConfig, ...config }, caption),
    validate: validateAction,
    toRuntimeDto: (createdAction: MicroflowAction) => ({
      nodeId: createdAction.id,
      type: "activity",
      kind: "activity",
      activityType: input.legacyActivityType,
      title: createdAction.caption ?? input.title,
      config: {
        kind: "action",
        objectId: createdAction.id,
        actionId: createdAction.id,
        actionKind: createdAction.kind,
        officialType: createdAction.officialType,
        availability,
        unsupported: availability === "requiresConnector" || availability === "nanoflowOnlyDisabled",
        deprecated: availability === "deprecated",
        authoringAction: createdAction
      }
    })
  };
  return item;
}

export const defaultMicroflowActionRegistry: MicroflowActionRegistryItem[] = [
  action({ key: "retrieve", legacyActivityType: "objectRetrieve", officialType: "Microflows$RetrieveAction", title: "Retrieve Object(s)", titleZh: "检索对象", description: "Retrieves one or more objects from database or association.", category: "object", defaultConfig: createDefaultActionConfig("retrieve") }),
  action({ key: "createObject", legacyActivityType: "objectCreate", officialType: "Microflows$CreateObjectAction", title: "Create Object", titleZh: "创建对象", description: "Creates an object instance and stores it in a variable.", category: "object", defaultConfig: createDefaultActionConfig("createObject") }),
  action({ key: "changeMembers", legacyActivityType: "objectChange", officialType: "Microflows$ChangeMembersAction", title: "Change Object", titleZh: "修改对象", description: "Changes member values on an existing object.", category: "object", defaultConfig: createDefaultActionConfig("changeMembers") }),
  action({ key: "commit", legacyActivityType: "objectCommit", officialType: "Microflows$CommitAction", title: "Commit Object(s)", titleZh: "提交对象", description: "Persists one object or object list.", category: "object", defaultConfig: createDefaultActionConfig("commit") }),
  action({ key: "delete", legacyActivityType: "objectDelete", officialType: "Microflows$DeleteAction", title: "Delete Object(s)", titleZh: "删除对象", description: "Deletes one object or object list.", category: "object", defaultConfig: createDefaultActionConfig("delete") }),
  action({ key: "rollback", legacyActivityType: "objectRollback", officialType: "Microflows$RollbackAction", title: "Rollback Object", titleZh: "回滚对象", description: "Rolls back uncommitted object changes.", category: "object", defaultConfig: createDefaultActionConfig("rollback") }),
  action({ key: "cast", legacyActivityType: "objectCast", officialType: "Microflows$CastAction", title: "Cast Object", titleZh: "转换对象", description: "Casts a generalized object to a specialization.", category: "object", defaultConfig: createDefaultActionConfig("cast") }),
  action({ key: "aggregateList", legacyActivityType: "listAggregate", officialType: "Microflows$AggregateListAction", title: "Aggregate List", titleZh: "列表聚合", description: "Aggregates a list with count, sum, average, min, or max.", category: "list", defaultConfig: createDefaultActionConfig("aggregateList") }),
  action({ key: "createList", legacyActivityType: "listCreate", officialType: "Microflows$CreateListAction", title: "Create List", titleZh: "创建列表", description: "Creates an empty typed list variable.", category: "list", defaultConfig: createDefaultActionConfig("createList") }),
  action({ key: "changeList", legacyActivityType: "listChange", officialType: "Microflows$ChangeListAction", title: "Change List", titleZh: "修改列表", description: "Adds, removes, clears, or replaces list contents.", category: "list", defaultConfig: createDefaultActionConfig("changeList") }),
  action({ key: "listOperation", legacyActivityType: "listOperation", officialType: "Microflows$ListOperationAction", title: "List Operation", titleZh: "列表操作", description: "Filters, sorts, combines, or selects list items.", category: "list", defaultConfig: createDefaultActionConfig("listOperation") }),
  action({ key: "callMicroflow", legacyActivityType: "callMicroflow", officialType: "Microflows$MicroflowCallAction", title: "Call Microflow", titleZh: "调用微流", description: "Calls another microflow with parameter mapping.", category: "call", defaultConfig: createDefaultActionConfig("callMicroflow") }),
  action({ key: "callJavaAction", legacyActivityType: "callJavaAction", officialType: "Microflows$JavaActionCallAction", title: "Call Java Action", titleZh: "调用 Java 动作", description: "Calls a server-side Java action.", category: "call" }),
  action({ key: "callJavaScriptAction", legacyActivityType: "callJavaScriptAction", officialType: "Microflows$JavaScriptActionCallAction", title: "Call JavaScript Action", titleZh: "调用 JavaScript 动作", description: "Nanoflow-only JavaScript action, disabled in Microflow.", category: "call", availability: "nanoflowOnlyDisabled" }),
  action({ key: "callNanoflow", legacyActivityType: "callNanoflow", officialType: "Microflows$NanoflowCallAction", title: "Call Nanoflow", titleZh: "调用纳流", description: "Nanoflow-only call node, disabled in Microflow.", category: "call", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false }),
  action({ key: "createVariable", legacyActivityType: "variableCreate", officialType: "Microflows$CreateVariableAction", title: "Create Variable", titleZh: "创建变量", description: "Creates a local microflow variable.", category: "variable", defaultConfig: createDefaultActionConfig("createVariable"), supportsErrorHandling: false }),
  action({ key: "changeVariable", legacyActivityType: "variableChange", officialType: "Microflows$ChangeVariableAction", title: "Change Variable", titleZh: "修改变量", description: "Changes the value of an existing variable.", category: "variable", defaultConfig: createDefaultActionConfig("changeVariable"), supportsErrorHandling: false }),
  action({ key: "closePage", legacyActivityType: "closePage", officialType: "Microflows$ClosePageAction", title: "Close Page", titleZh: "关闭页面", description: "Closes the current or last opened page.", category: "client", supportsErrorHandling: false }),
  action({ key: "downloadFile", legacyActivityType: "downloadFile", officialType: "Microflows$DownloadFileAction", title: "Download File", titleZh: "下载文件", description: "Downloads a file document in the browser.", category: "client", supportsErrorHandling: false }),
  action({ key: "showHomePage", legacyActivityType: "showHomePage", officialType: "Microflows$ShowHomePageAction", title: "Show Home Page", titleZh: "显示首页", description: "Navigates the user to the home page.", category: "client", supportsErrorHandling: false }),
  action({ key: "showMessage", legacyActivityType: "showMessage", officialType: "Microflows$ShowMessageAction", title: "Show Message", titleZh: "显示消息", description: "Displays a blocking or non-blocking message.", category: "client", supportsErrorHandling: false }),
  action({ key: "showPage", legacyActivityType: "showPage", officialType: "Microflows$ShowPageAction", title: "Show Page", titleZh: "显示页面", description: "Opens a page for the current user.", category: "client", supportsErrorHandling: false }),
  action({ key: "validationFeedback", legacyActivityType: "validationFeedback", officialType: "Microflows$ValidationFeedbackAction", title: "Validation Feedback", titleZh: "验证反馈", description: "Shows validation feedback under a page field.", category: "client", supportsErrorHandling: false }),
  action({ key: "synchronize", legacyActivityType: "synchronize", officialType: "Microflows$SynchronizeAction", title: "Synchronize", titleZh: "同步", description: "Nanoflow-only synchronize action, disabled in Microflow.", category: "client", availability: "nanoflowOnlyDisabled", supportsErrorHandling: false }),
  action({ key: "restCall", legacyActivityType: "callRest", officialType: "Microflows$RestCallAction", title: "Call REST Service", titleZh: "调用 REST 服务", description: "Calls a REST endpoint with request and response mapping.", category: "integration", defaultConfig: createDefaultActionConfig("restCall") }),
  action({ key: "webServiceCall", legacyActivityType: "callWebService", officialType: "Microflows$WebServiceCallAction", title: "Call Web Service", titleZh: "调用 Web Service", description: "Calls an imported SOAP/Web Service.", category: "integration" }),
  action({ key: "importXml", legacyActivityType: "importWithMapping", officialType: "Microflows$ImportXmlAction", title: "Import Mapping", titleZh: "导入映射", description: "Imports XML or JSON through an import mapping.", category: "integration" }),
  action({ key: "exportXml", legacyActivityType: "exportWithMapping", officialType: "Microflows$ExportXmlAction", title: "Export Mapping", titleZh: "导出映射", description: "Exports objects through an export mapping.", category: "integration" }),
  action({ key: "callExternalAction", legacyActivityType: "callExternalAction", officialType: "Microflows$CallExternalAction", title: "Call External Action", titleZh: "调用外部动作", description: "Calls an external action from a connector.", category: "integration", availability: "requiresConnector" }),
  action({ key: "restOperationCall", legacyActivityType: "sendRestRequestBeta", officialType: "Microflows$RestOperationCallAction", title: "REST Operation Call", titleZh: "REST 操作调用", description: "Calls an operation from a consumed REST service document.", category: "integration", availability: "beta" }),
  action({ key: "logMessage", legacyActivityType: "logMessage", officialType: "Microflows$LogMessageAction", title: "Log Message", titleZh: "记录日志", description: "Writes an application log entry.", category: "logging", supportsErrorHandling: false }),
  action({ key: "generateDocument", legacyActivityType: "generateDocument", officialType: "Microflows$GenerateDocumentAction", title: "Generate Document", titleZh: "生成文档", description: "Generates a document from a template for legacy compatibility.", category: "documentGeneration", availability: "deprecated" }),
  action({ key: "counter", legacyActivityType: "counter", officialType: "Microflows$CounterAction", title: "Counter", titleZh: "计数器", description: "Sets a custom counter metric value.", category: "metrics", supportsErrorHandling: false }),
  action({ key: "incrementCounter", legacyActivityType: "incrementCounter", officialType: "Microflows$IncrementCounterAction", title: "Increment Counter", titleZh: "计数器加一", description: "Increments a counter metric by one.", category: "metrics", supportsErrorHandling: false }),
  action({ key: "gauge", legacyActivityType: "gauge", officialType: "Microflows$GaugeAction", title: "Gauge", titleZh: "仪表指标", description: "Sets a gauge metric value.", category: "metrics", supportsErrorHandling: false }),
  action({ key: "mlModelCall", legacyActivityType: "callMlModel", officialType: "Microflows$MLModelCallAction", title: "Call ML Model", titleZh: "调用 ML 模型", description: "Calls an ML model mapping.", category: "mlKit" }),
  action({ key: "applyJumpToOption", legacyActivityType: "applyJumpToOption", officialType: "Microflows$ApplyJumpToOptionAction", title: "Apply Jump-To Option", titleZh: "应用跳转选项", description: "Applies a generated workflow jump-to option.", category: "workflow" }),
  action({ key: "callWorkflow", legacyActivityType: "callWorkflow", officialType: "Microflows$CallWorkflowAction", title: "Call Workflow", titleZh: "调用工作流", description: "Starts a target workflow with a context object.", category: "workflow" }),
  action({ key: "changeWorkflowState", legacyActivityType: "changeWorkflowState", officialType: "Microflows$ChangeWorkflowStateAction", title: "Change Workflow State", titleZh: "修改工作流状态", description: "Changes workflow state such as abort, continue, pause, retry.", category: "workflow" }),
  action({ key: "completeUserTask", legacyActivityType: "completeUserTask", officialType: "Microflows$CompleteUserTaskAction", title: "Complete User Task", titleZh: "完成用户任务", description: "Completes a user task with an outcome.", category: "workflow" }),
  action({ key: "generateJumpToOptions", legacyActivityType: "generateJumpToOptions", officialType: "Microflows$GenerateJumpToOptionsAction", title: "Generate Jump-To Options", titleZh: "生成跳转选项", description: "Generates available workflow jump-to options.", category: "workflow" }),
  action({ key: "retrieveWorkflowActivityRecords", legacyActivityType: "retrieveWorkflowActivityRecords", officialType: "Microflows$RetrieveWorkflowActivityRecordsAction", title: "Retrieve Workflow Activity Records", titleZh: "检索工作流活动记录", description: "Retrieves activity records for a workflow instance.", category: "workflow" }),
  action({ key: "retrieveWorkflowContext", legacyActivityType: "retrieveWorkflowContext", officialType: "Microflows$RetrieveWorkflowContextAction", title: "Retrieve Workflow Context", titleZh: "检索工作流上下文", description: "Retrieves the context object for a workflow instance.", category: "workflow" }),
  action({ key: "retrieveWorkflows", legacyActivityType: "retrieveWorkflows", officialType: "Microflows$RetrieveWorkflowsAction", title: "Retrieve Workflows", titleZh: "检索工作流", description: "Retrieves workflows by context and filters.", category: "workflow" }),
  action({ key: "showUserTaskPage", legacyActivityType: "showUserTaskPage", officialType: "Microflows$ShowUserTaskPageAction", title: "Show User Task Page", titleZh: "显示用户任务页面", description: "Opens the configured user task page.", category: "workflow", supportsErrorHandling: false }),
  action({ key: "showWorkflowAdminPage", legacyActivityType: "showWorkflowAdminPage", officialType: "Microflows$ShowWorkflowAdminPageAction", title: "Show Workflow Admin Page", titleZh: "显示工作流管理页", description: "Opens the workflow admin page.", category: "workflow", supportsErrorHandling: false }),
  action({ key: "lockWorkflow", legacyActivityType: "lockWorkflow", officialType: "Microflows$LockWorkflowAction", title: "Lock Workflow", titleZh: "锁定工作流", description: "Locks a workflow instance.", category: "workflow", supportsErrorHandling: false }),
  action({ key: "unlockWorkflow", legacyActivityType: "unlockWorkflow", officialType: "Microflows$UnlockWorkflowAction", title: "Unlock Workflow", titleZh: "解锁工作流", description: "Unlocks a workflow instance.", category: "workflow", supportsErrorHandling: false }),
  action({ key: "notifyWorkflow", legacyActivityType: "notifyWorkflow", officialType: "Microflows$NotifyWorkflowAction", title: "Notify Workflow", titleZh: "通知工作流", description: "Notifies a waiting workflow instance.", category: "workflow", supportsErrorHandling: false }),
  action({ key: "deleteExternalObject", legacyActivityType: "deleteExternalObject", officialType: "Microflows$DeleteExternalObjectAction", title: "Delete External Object", titleZh: "删除外部对象", description: "Deletes an external object through a service operation.", category: "externalObject", availability: "requiresConnector" }),
  action({ key: "sendExternalObject", legacyActivityType: "sendExternalObject", officialType: "Microflows$SendExternalObjectAction", title: "Send External Object", titleZh: "发送外部对象", description: "Sends or updates an external object through a service operation.", category: "externalObject", availability: "requiresConnector" })
];

export const microflowActionRegistryByKind = new Map(defaultMicroflowActionRegistry.map(entry => [entry.kind, entry]));
export const microflowActionRegistryByActivityType = new Map(defaultMicroflowActionRegistry.map(entry => [entry.legacyActivityType, entry]));
