import type {
  LegacyMicroflowActivityConfig,
  MicroflowActionKind,
  MicroflowActivityType,
  MicroflowDataType,
  MicroflowExpression
} from "../schema/types";

export interface MicroflowNodeCreateContext {
  microflowId?: string;
  moduleId?: string;
  workspaceId?: string;
  position?: { x: number; y: number };
  source?: "drag" | "click" | "paste" | "duplicate";
  schemaLoaded?: boolean;
  metadataAvailable?: boolean;
  readonly?: boolean;
  supportedActionKinds?: string[];
}

type SafeDefaultActivityConfig = Partial<LegacyMicroflowActivityConfig> & Record<string, unknown>;

const stringType: MicroflowDataType = { kind: "string" };
const stringTypeRef = { kind: "primitive" as const, name: "String" };
const integerType: MicroflowDataType = { kind: "integer" };
const booleanType: MicroflowDataType = { kind: "boolean" };
const unknownType: MicroflowDataType = { kind: "unknown", reason: "registry default" };

export function createDefaultExpression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

export function createDefaultActionConfig(
  actionKind: MicroflowActionKind,
  _context?: MicroflowNodeCreateContext
): SafeDefaultActivityConfig {
  switch (actionKind) {
    case "retrieve":
      return { entity: "", resultVariableName: "" };
    case "createObject":
      return { entity: "", objectVariableName: "" };
    case "changeMembers":
    case "commit":
    case "delete":
    case "rollback":
    case "cast":
      return { objectVariableName: "" };
    case "aggregateList":
      return { listVariableName: "", sourceListVariableName: "", operation: "count", aggregateFunction: "count", resultVariableName: "", outputVariableName: "" };
    case "createList":
      return {
        entity: "",
        entityQualifiedName: "",
        listVariableName: "",
        outputListVariableName: "",
        elementType: stringType,
        itemType: stringType,
        listType: "mutable",
        initialItemsExpression: createDefaultExpression("", { kind: "list", itemType: stringType }),
        description: ""
      };
    case "changeList":
      return {
        listVariableName: "",
        targetListVariableName: "",
        operation: "add",
        objectVariableName: "",
        itemExpression: createDefaultExpression(""),
        itemsExpression: createDefaultExpression("", { kind: "list", itemType: unknownType }),
        conditionExpression: createDefaultExpression("", booleanType),
        indexExpression: createDefaultExpression("", integerType)
      };
    case "listOperation":
      return {
        listVariableName: "",
        leftListVariableName: "",
        sourceListVariableName: "",
        operation: "filter",
        expression: createDefaultExpression("", booleanType),
        filterExpression: createDefaultExpression("", booleanType),
        sortExpression: createDefaultExpression(""),
        resultVariableName: "",
        outputVariableName: "",
        outputListVariableName: "",
        outputElementType: stringType
      };
    case "callMicroflow":
      return {
        targetMicroflowId: "",
        targetMicroflowName: "",
        targetMicroflowQualifiedName: "",
        parameterMappings: [],
        returnValueTarget: "",
        configurationState: "incomplete"
      };
    case "callWorkflow":
      return { targetWorkflowId: "", objectVariableName: "", outputWorkflowVariableName: "" };
    case "callJavaAction":
    case "callJavaScriptAction":
    case "callNanoflow":
      return { targetMicroflowId: "", parameterMappings: [], returnValue: { storeResult: false, outputVariableName: "" } };
    case "createVariable":
      return { variableName: "newVariable", variableType: stringTypeRef, initialValueExpression: "" };
    case "changeVariable":
      return { variableName: "", newValueExpression: "" };
    case "restCall":
      return { method: "GET", url: "", headers: [], queryParams: [], body: "", responseVariableName: "" };
    case "showMessage":
      return { messageType: "information", blocking: false, messageExpression: createDefaultExpression("", stringType) };
    case "showPage":
      return { pageName: "", pageId: "", pageParameterMappings: [], openMode: "current", title: "" };
    case "closePage":
      return { closeMode: "current" };
    case "downloadFile":
      return { objectVariableName: "" };
    case "validationFeedback":
      return { objectVariableName: "", targetMember: "", feedbackMessage: "" };
    case "webServiceCall":
      return { serviceId: "", operation: "" };
    case "callExternalAction":
      return { serviceId: "", externalActionId: "", parameterMappings: [], returnVariableName: "" };
    case "importXml":
      return { sourceVariableName: "", mappingId: "", outputVariableName: "" };
    case "exportXml":
      return { sourceVariableName: "", mappingId: "", outputType: "string", outputVariableName: "" };
    case "restOperationCall":
      return { serviceId: "", operation: "", parameterMappings: [], outputVariableName: "" };
    case "logMessage":
      return {
        logLevel: "info",
        messageExpression: createDefaultExpression("", stringType),
        logNodeName: "Microflow"
      };
    case "generateDocument":
      return { mappingId: "", objectVariableName: "" };
    case "counter":
      return { metricName: "", valueExpression: createDefaultExpression("1", integerType), tags: [] };
    case "incrementCounter":
      return { metricName: "", tags: [] };
    case "gauge":
      return { metricName: "", valueExpression: createDefaultExpression("0", integerType), tags: [] };
    case "mlModelCall":
      return { mappingId: "", outputVariableName: "" };
    case "applyJumpToOption":
      return { workflowInstanceVariable: "", objectVariableName: "", outputVariableName: "" };
    case "changeWorkflowState":
      return { workflowInstanceVariable: "", operation: "pause", reason: "" };
    case "completeUserTask":
      return { objectVariableName: "", operation: "outcome" };
    case "generateJumpToOptions":
      return { workflowInstanceVariable: "", resultVariableName: "" };
    case "retrieveWorkflowActivityRecords":
      return { workflowInstanceVariable: "", listVariableName: "" };
    case "retrieveWorkflowContext":
      return { workflowInstanceVariable: "", entity: "", objectVariableName: "" };
    case "retrieveWorkflows":
      return { objectVariableName: "", listVariableName: "" };
    case "showUserTaskPage":
      return { objectVariableName: "" };
    case "showWorkflowAdminPage":
    case "lockWorkflow":
    case "unlockWorkflow":
    case "notifyWorkflow":
      return { workflowInstanceVariable: "" };
    case "deleteExternalObject":
    case "sendExternalObject":
      return { objectVariableName: "", operation: "" };
    case "synchronize":
    case "showHomePage":
      return {};
    default:
      return { dataType: unknownType };
  }
}

export function createDefaultActivityConfig(
  activityType: MicroflowActivityType,
  context?: MicroflowNodeCreateContext
): SafeDefaultActivityConfig {
  if (activityType === "queryExternalDatabase") {
    return { connectorId: "", operation: "query" };
  }
  const map: Partial<Record<MicroflowActivityType, MicroflowActionKind>> = {
    objectCast: "cast",
    objectCreate: "createObject",
    objectChange: "changeMembers",
    objectCommit: "commit",
    objectDelete: "delete",
    objectRetrieve: "retrieve",
    objectRollback: "rollback",
    listAggregate: "aggregateList",
    listCreate: "createList",
    listChange: "changeList",
    listOperation: "listOperation",
    callMicroflow: "callMicroflow",
    callJavaAction: "callJavaAction",
    callJavaScriptAction: "callJavaScriptAction",
    callNanoflow: "callNanoflow",
    variableCreate: "createVariable",
    variableChange: "changeVariable",
    callRest: "restCall",
    callWebService: "webServiceCall",
    callExternalAction: "callExternalAction",
    importWithMapping: "importXml",
    exportWithMapping: "exportXml",
    sendRestRequestBeta: "restOperationCall",
    logMessage: "logMessage",
    showPage: "showPage",
    closePage: "closePage",
    downloadFile: "downloadFile",
    showHomePage: "showHomePage",
    showMessage: "showMessage",
    synchronizeToDevice: "synchronize",
    validationFeedback: "validationFeedback",
    synchronize: "synchronize",
    generateDocument: "generateDocument",
    counter: "counter",
    incrementCounter: "incrementCounter",
    gauge: "gauge",
    callMlModel: "mlModelCall",
    applyJumpToOption: "applyJumpToOption",
    callWorkflow: "callWorkflow",
    changeWorkflowState: "changeWorkflowState",
    completeUserTask: "completeUserTask",
    generateJumpToOptions: "generateJumpToOptions",
    retrieveWorkflowActivityRecords: "retrieveWorkflowActivityRecords",
    retrieveWorkflowContext: "retrieveWorkflowContext",
    retrieveWorkflows: "retrieveWorkflows",
    showUserTaskPage: "showUserTaskPage",
    showWorkflowAdminPage: "showWorkflowAdminPage",
    lockWorkflow: "lockWorkflow",
    unlockWorkflow: "unlockWorkflow",
    notifyWorkflow: "notifyWorkflow",
    deleteExternalObject: "deleteExternalObject",
    sendExternalObject: "sendExternalObject"
  };
  return createDefaultActionConfig(map[activityType] ?? "logMessage", context);
}
