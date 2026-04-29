/**
 * @deprecated Legacy graph node kind (demo editor only). Use {@link MicroflowObject} / {@link MicroflowObjectKind} in production.
 */
export type LegacyMicroflowNodeKind =
  | "event"
  | "decision"
  | "objectTypeDecision"
  | "merge"
  | "loop"
  | "parameter"
  | "annotation"
  | "activity"
  | "gateway"
  | "tryCatch"
  | "errorHandler";

/**
 * @deprecated Legacy graph node type (demo editor only). Use {@link MicroflowObjectKind} for persisted microflows.
 */
export type LegacyMicroflowNodeType =
  | "startEvent"
  | "endEvent"
  | "errorEvent"
  | "breakEvent"
  | "continueEvent"
  | "decision"
  | "objectTypeDecision"
  | "merge"
  | "loop"
  | "parameter"
  | "annotation"
  | "activity"
  | "parallelGateway"
  | "inclusiveGateway"
  | "tryCatch"
  | "errorHandler";

export type MicroflowNodeCategory =
  | "events"
  | "flows"
  | "decisions"
  | "activities"
  | "loop"
  | "parameters"
  | "annotations";

export type MicroflowActivityCategory =
  | "object"
  | "list"
  | "call"
  | "variable"
  | "client"
  | "integration"
  | "logging"
  | "documentGeneration"
  | "metrics"
  | "mlKit"
  | "workflow"
  | "externalObject";

export type MicroflowNodeAvailability =
  | "supported"
  | "beta"
  | "deprecated"
  | "requiresConnector"
  | "nanoflowOnlyDisabled";

export type MicroflowActivityType =
  | "objectCast"
  | "objectCreate"
  | "objectChange"
  | "objectCommit"
  | "objectDelete"
  | "objectRetrieve"
  | "objectRollback"
  | "listCreate"
  | "listChange"
  | "listOperation"
  | "listAggregate"
  | "variableCreate"
  | "variableChange"
  | "callMicroflow"
  | "callJavaAction"
  | "callJavaScriptAction"
  | "callNanoflow"
  | "callRest"
  | "callWebService"
  | "callExternalAction"
  | "importWithMapping"
  | "exportWithMapping"
  | "queryExternalDatabase"
  | "sendRestRequestBeta"
  | "logMessage"
  | "showPage"
  | "closePage"
  | "downloadFile"
  | "showHomePage"
  | "showMessage"
  | "synchronizeToDevice"
  | "validationFeedback"
  | "synchronize"
  | "generateDocument"
  | "counter"
  | "incrementCounter"
  | "gauge"
  | "callMlModel"
  | "applyJumpToOption"
  | "callWorkflow"
  | "changeWorkflowState"
  | "completeUserTask"
  | "generateJumpToOptions"
  | "retrieveWorkflowActivityRecords"
  | "retrieveWorkflowContext"
  | "retrieveWorkflows"
  | "showUserTaskPage"
  | "showWorkflowAdminPage"
  | "lockWorkflow"
  | "unlockWorkflow"
  | "notifyWorkflow"
  | "deleteExternalObject"
  | "sendExternalObject"
  | "throwException"
  | "listFilter"
  | "listSort";

export type MicroflowEdgeKind =
  | "sequence"
  | "decisionCondition"
  | "objectTypeCondition"
  | "errorHandler"
  | "annotation";
export type MicroflowEdgeType = MicroflowEdgeKind;
export type MicroflowEdgeStyle = "solid" | "dashed" | "dotted";
export type MicroflowConditionValue =
  | { kind: "boolean"; value: true | false }
  | { kind: "enumeration"; value: string | "empty" }
  | { kind: "objectType"; entity: string | "empty" | "fallback" }
  | { kind: "custom"; value: string };
export type MicroflowErrorHandlingType =
  | "rollback"
  | "customWithRollback"
  | "customWithoutRollback"
  | "continue";
export type MicroflowResourceStatus = "draft" | "published" | "archived";
export type MicroflowResourceScope = "all" | "mine" | "shared" | "favorite";
export type MicroflowResourceSortKey = "updatedAt" | "createdAt" | "name" | "version";
export type MicroflowPortDirection = "input" | "output";
export type MicroflowPortKind =
  | "sequenceIn"
  | "sequenceOut"
  | "decisionOut"
  | "objectTypeOut"
  | "errorOut"
  | "annotation"
  | "loopIn"
  | "loopOut"
  | "loopBodyIn"
  | "loopBodyOut";
export type MicroflowPortCardinality = "none" | "one" | "zeroOrOne" | "oneOrMore" | "zeroOrMore";
export type MicroflowExpressionLanguage = "mendix" | "javascript" | "plainText";
export type MicroflowErrorHandlingMode = MicroflowErrorHandlingType;
export type MicroflowPropertyTabKey = "properties" | "documentation" | "errorHandling" | "output" | "advanced";

export interface MicroflowPosition {
  x: number;
  y: number;
}

export interface MicroflowPort {
  id: string;
  label: string;
  direction: MicroflowPortDirection;
  kind: MicroflowPortKind;
  cardinality: MicroflowPortCardinality;
  edgeTypes: MicroflowEdgeType[];
}

export interface MicroflowTypeRef {
  kind: "primitive" | "entity" | "list" | "object" | "void" | "unknown";
  name: string;
  entity?: string;
  itemType?: MicroflowTypeRef;
  nullable?: boolean;
}

export interface MicroflowExpression {
  id?: string;
  language?: MicroflowExpressionLanguage;
  text?: string;
  raw: string;
  expectedType?: MicroflowTypeRef;
  referencedVariables?: string[];
  inferredType?: MicroflowDataType;
  references?: {
    variables: string[];
    entities: string[];
    attributes: string[];
    associations: string[];
    enumerations: string[];
    functions: string[];
  };
  diagnostics?: MicroflowExpressionDiagnostic[];
}

export interface MicroflowVariable {
  id: string;
  name: string;
  type: MicroflowTypeRef;
  scope: "microflow" | "node" | "latestError";
  defaultValue?: MicroflowExpression;
}

export interface MicroflowParameter {
  id: string;
  stableId?: string;
  name: string;
  dataType: MicroflowDataType;
  type?: MicroflowTypeRef;
  required: boolean;
  documentation?: string;
  description?: string;
  defaultValue?: MicroflowExpression;
  exampleValue?: string;
}

export interface MicroflowErrorHandling {
  mode: MicroflowErrorHandlingMode;
  errorVariableName?: string;
  targetNodeId?: string;
}

export interface MicroflowRenderMetadata {
  iconKey: string;
  shape: "event" | "roundedRect" | "diamond" | "loop" | "annotation";
  tone: "neutral" | "success" | "warning" | "danger" | "info";
  width?: number;
  height?: number;
}

export interface MicroflowPropertyFormMetadata {
  formKey: string;
  sections: string[];
}

export interface MicroflowNodeDocumentation {
  summary?: string;
  whenToUse?: string;
  examples?: string[];
  business?: string;
  technical?: string;
  inputs?: string;
  outputs?: string;
  notes?: string;
  example?: string;
  tags?: string[];
}

export interface MicroflowNodeOutput {
  id: string;
  name: string;
  type: MicroflowTypeRef;
  source: string;
  downstreamAvailable: boolean;
}

export interface MicroflowNodeAdvancedConfig {
  timeoutMs?: number;
  retryEnabled?: boolean;
  retryCount?: number;
  retryIntervalMs?: number;
  verboseLogging?: boolean;
  ignoreNonCriticalErrors?: boolean;
  permissionContext?: string;
  transactionBoundary?: "inherit" | "requiresNew" | "none";
  performanceTag?: string;
  followRedirects?: boolean;
}

export interface MicroflowAttributeAssignment {
  id: string;
  attribute: string;
  expression: MicroflowExpression;
}

/**
 * @deprecated Legacy graph schema used only for migration from the old demo editor.
 * Do not use as runtime, resource, FlowGram, validator, or backend contract schema.
 */
export interface LegacyMicroflowNodeBase<TKind extends LegacyMicroflowNodeType = LegacyMicroflowNodeType, TConfig extends object = Record<string, unknown>> {
  id: string;
  type: TKind;
  kind?: LegacyMicroflowNodeKind;
  title: string;
  titleZh?: string;
  description?: string;
  alias?: string;
  enabled?: boolean;
  locked?: boolean;
  documentation?: MicroflowNodeDocumentation;
  tags?: string[];
  outputs?: MicroflowNodeOutput[];
  advanced?: MicroflowNodeAdvancedConfig;
  category: MicroflowNodeCategory;
  position: MicroflowPosition;
  ports: MicroflowPort[];
  config: TConfig;
  render: MicroflowRenderMetadata;
  propertyForm: MicroflowPropertyFormMetadata;
  availability?: MicroflowNodeAvailability;
  availabilityReason?: string;
  parentLoopId?: string;
  validation?: {
    disabled?: boolean;
  };
}

export interface MicroflowEventConfig {
  returnValue?: MicroflowExpression;
  errorName?: string;
  startTrigger?: "manual" | "pageEvent" | "formSubmit" | "workflowCall" | "apiCall";
  allowExternalCall?: boolean;
  logExecution?: boolean;
  endType?: "normal" | "returnValue" | "throwError";
  returnType?: MicroflowTypeRef;
  returnVariableName?: string;
}

export interface MicroflowDecisionConfig {
  expression: MicroflowExpression;
  decisionType?: "expression" | "rule";
  ruleReference?: string;
  resultType?: "Boolean" | "Enumeration";
  branches?: Array<{ conditionValue?: MicroflowConditionValue; label?: string; targetNodeId?: string }>;
}

export interface MicroflowObjectTypeDecisionConfig {
  inputObject: string;
  generalizedEntity?: string;
  branches?: Array<{ conditionValue?: MicroflowConditionValue; label?: string; targetNodeId?: string }>;
}

export interface MicroflowMergeConfig {
  strategy: "firstAvailable" | "all";
}

export interface MicroflowLoopConfig {
  iterableVariableName: string;
  itemVariableName: string;
  loopType?: "list" | "condition" | "forEach" | "while";
  indexVariableName?: string;
  whileExpression?: MicroflowExpression;
  skipWhenEmpty?: boolean;
  note?: string;
}

/**
 * @deprecated Legacy activity config bag for demo graph nodes. Authoring uses {@link MicroflowAction} on {@link MicroflowActionActivity}.
 */
export interface LegacyMicroflowActivityConfig {
  activityType: MicroflowActivityType;
  activityCategory?: MicroflowActivityCategory;
  entity?: string;
  association?: string;
  objectVariableName?: string;
  listVariableName?: string;
  variableName?: string;
  variableType?: MicroflowTypeRef;
  elementType?: MicroflowDataType;
  valueExpression?: MicroflowExpression;
  targetMicroflowId?: string;
  targetMicroflowName?: string;
  targetMicroflowQualifiedName?: string;
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url?: string;
  headers?: Array<{ key: string; value: string }>;
  query?: Array<{ key: string; value: string }>;
  bodyExpression?: MicroflowExpression;
  timeoutMs?: number;
  resultVariableName?: string;
  outputVariableName?: string;
  outputListVariableName?: string;
  outputVariableId?: string;
  logLevel?: "trace" | "debug" | "info" | "warn" | "error";
  messageExpression?: MicroflowExpression;
  pageName?: string;
  range?: "all" | "first" | "limit";
  limit?: number;
  sort?: string;
  refreshClient?: boolean;
  withEvents?: boolean;
  errorHandling?: MicroflowErrorHandling;
  supportsErrorFlow?: boolean;
  reserved?: boolean;
  retrieveMode?: "association" | "database";
  notFoundStrategy?: "empty" | "throw" | "errorFlow";
  sortRules?: Array<{ id: string; attribute: string; direction: "asc" | "desc" }>;
  assignments?: MicroflowAttributeAssignment[];
  commitImmediately?: boolean;
  validateObject?: boolean;
  deleteList?: boolean;
  deleteConfirmation?: string;
  rollbackScope?: "object" | "list" | "transaction";
  readonly?: boolean;
  callMode?: "sync" | "async";
  targetMicroflowDisplayName?: string;
  targetModuleId?: string;
  parameterMappings?: Array<{ id: string; targetParameterId?: string; targetParameterName?: string; parameterName: string; parameterType?: MicroflowDataType; targetType?: MicroflowDataType; argumentExpression?: MicroflowExpression; expression: MicroflowExpression; sourceVariableId?: string; sourceVariableName?: string }>;
  bodyType?: "none" | "json" | "form" | "text";
  responseMapping?: string;
  logContextVariables?: boolean;
  logTraceId?: boolean;
  pageOpenMode?: "current" | "modal" | "newWindow";
  pageTitle?: string;
  pageParameterMappings?: Array<{ id: string; parameterName: string; expression: MicroflowExpression }>;
  closeMode?: "current" | "modal" | "back";
  closeResult?: string;
  errorDescription?: string;
  errorLogEnabled?: boolean;
  customErrorMicroflowId?: string;
  connectorId?: string;
  operation?: string;
  aggregateFunction?: string;
  sourceVariableName?: string;
  sourceListVariableName?: string;
  targetListVariableName?: string;
  leftListVariableName?: string;
  targetEntity?: string;
  entityQualifiedName?: string;
  targetMember?: string;
  metricName?: string;
  workflowInstanceVariable?: string;
  externalActionId?: string;
  serviceId?: string;
  mappingId?: string;
  outputType?: string;
  tags?: Array<{ key: string; value: string }>;
}

export interface MicroflowParameterConfig {
  parameter: MicroflowParameter;
  defaultValue?: MicroflowExpression;
  exampleValue?: string;
}

export interface MicroflowAnnotationConfig {
  text: string;
  color?: string;
  title?: string;
  pinned?: boolean;
  exportToDocumentation?: boolean;
}

export type LegacyMicroflowEventNode = LegacyMicroflowNodeBase<
  "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent",
  MicroflowEventConfig
>;
export type LegacyMicroflowDecisionNode = LegacyMicroflowNodeBase<"decision", MicroflowDecisionConfig>;
export type LegacyMicroflowObjectTypeDecisionNode = LegacyMicroflowNodeBase<"objectTypeDecision", MicroflowObjectTypeDecisionConfig>;
export type LegacyMicroflowMergeNode = LegacyMicroflowNodeBase<"merge", MicroflowMergeConfig>;
export type LegacyMicroflowLoopNode = LegacyMicroflowNodeBase<"loop", MicroflowLoopConfig>;
export type LegacyMicroflowActivityNode = LegacyMicroflowNodeBase<"activity", LegacyMicroflowActivityConfig>;
export type LegacyMicroflowParameterNode = LegacyMicroflowNodeBase<"parameter", MicroflowParameterConfig>;
export type LegacyMicroflowAnnotationNode = LegacyMicroflowNodeBase<"annotation", MicroflowAnnotationConfig>;

/**
 * @deprecated Legacy demo graph node union. Migrate with {@link normalizeMicroflowSchema}.
 */
export type LegacyMicroflowNode =
  | LegacyMicroflowEventNode
  | LegacyMicroflowDecisionNode
  | LegacyMicroflowObjectTypeDecisionNode
  | LegacyMicroflowMergeNode
  | LegacyMicroflowLoopNode
  | LegacyMicroflowActivityNode
  | LegacyMicroflowParameterNode
  | LegacyMicroflowAnnotationNode;

/**
 * @deprecated Legacy demo graph edge. Authoring uses {@link MicroflowFlow}.
 */
export interface LegacyMicroflowEdgeBase<TKind extends MicroflowEdgeKind = MicroflowEdgeKind> {
  id: string;
  type: TKind;
  kind?: TKind;
  sourceNodeId: string;
  sourcePortId?: string;
  targetNodeId: string;
  targetPortId?: string;
  label?: string;
  description?: string;
  condition?: MicroflowExpression;
  conditionValue?: MicroflowConditionValue;
  isDefault?: boolean;
  branchOrder?: number;
  errorHandlingType?: MicroflowErrorHandlingType;
  exposeLatestError?: boolean;
  exposeLatestSoapFault?: boolean;
  exposeLatestHttpResponse?: boolean;
  targetErrorVariableName?: string;
  logError?: boolean;
  attachmentMode?: "node" | "edge" | "canvas";
  showInExport?: boolean;
  bendPoints?: MicroflowPosition[];
}

export type LegacyMicroflowSequenceEdge = LegacyMicroflowEdgeBase<"sequence">;
export type LegacyMicroflowDecisionConditionEdge = LegacyMicroflowEdgeBase<"decisionCondition">;
export type LegacyMicroflowObjectTypeConditionEdge = LegacyMicroflowEdgeBase<"objectTypeCondition">;
export type LegacyMicroflowErrorHandlerEdge = LegacyMicroflowEdgeBase<"errorHandler">;
export type LegacyMicroflowAnnotationEdge = LegacyMicroflowEdgeBase<"annotation">;
/**
 * @deprecated Legacy demo graph edge union.
 */
export type LegacyMicroflowEdge =
  | LegacyMicroflowSequenceEdge
  | LegacyMicroflowDecisionConditionEdge
  | LegacyMicroflowObjectTypeConditionEdge
  | LegacyMicroflowErrorHandlerEdge
  | LegacyMicroflowAnnotationEdge;

export type MicroflowObjectKind =
  | "startEvent"
  | "endEvent"
  | "errorEvent"
  | "breakEvent"
  | "continueEvent"
  | "exclusiveSplit"
  | "inheritanceSplit"
  | "exclusiveMerge"
  | "actionActivity"
  | "loopedActivity"
  | "parameterObject"
  | "annotation"
  | "parallelGateway"
  | "inclusiveGateway"
  | "tryCatch"
  | "errorHandler";

export type MicroflowDataType =
  | { kind: "void" }
  | { kind: "boolean" }
  | { kind: "integer" }
  | { kind: "long" }
  | { kind: "decimal" }
  | { kind: "string" }
  | { kind: "dateTime" }
  | { kind: "enumeration"; enumerationQualifiedName: string }
  | { kind: "object"; entityQualifiedName: string }
  | { kind: "list"; itemType: MicroflowDataType }
  | { kind: "fileDocument"; entityQualifiedName?: string }
  | { kind: "binary" }
  | { kind: "json" }
  | { kind: "unknown"; reason?: string };

export interface MicroflowExpressionDiagnostic {
  id?: string;
  severity: "error" | "warning" | "info";
  code?: string;
  message: string;
  range?: {
    start: number;
    end: number;
  };
  variableName?: string;
  memberName?: string;
  expectedType?: MicroflowDataType;
  actualType?: MicroflowDataType;
}

export interface MicroflowPoint {
  x: number;
  y: number;
}

export interface MicroflowSize {
  width: number;
  height: number;
}

export interface MicroflowLine {
  kind: "orthogonal" | "polyline" | "bezier";
  points: MicroflowPoint[];
  routing: {
    mode: "auto" | "manual";
    bendPoints: MicroflowPoint[];
  };
  style: {
    strokeType: "solid" | "dashed" | "dotted";
    strokeWidth: number;
    arrow: "none" | "target" | "source" | "both";
  };
}

export interface MicroflowObjectBase {
  id: string;
  stableId: string;
  kind: MicroflowObjectKind;
  officialType: string;
  caption?: string;
  documentation?: string;
  relativeMiddlePoint: MicroflowPoint;
  size: MicroflowSize;
  disabled?: boolean;
  editor: {
    zIndex?: number;
    selected?: boolean;
    collapsed?: boolean;
    colorToken?: string;
    iconKey?: string;
    laneId?: string;
  };
}

export interface MicroflowStartEvent extends MicroflowObjectBase {
  kind: "startEvent";
  officialType: "Microflows$StartEvent";
  trigger: {
    type: "manual" | "pageEvent" | "formSubmit" | "workflowCall" | "apiCall" | "scheduled" | "system";
  };
}

export interface MicroflowEndEvent extends MicroflowObjectBase {
  kind: "endEvent";
  officialType: "Microflows$EndEvent";
  returnValue?: MicroflowExpression;
  endBehavior: {
    type: "normalReturn";
  };
}

export interface MicroflowErrorEvent extends MicroflowObjectBase {
  kind: "errorEvent";
  officialType: "Microflows$ErrorEvent";
  error: {
    sourceVariableName: "$latestError";
    messageExpression?: MicroflowExpression;
  };
}

export interface MicroflowBreakEvent extends MicroflowObjectBase {
  kind: "breakEvent";
  officialType: "Microflows$BreakEvent";
  targetLoopObjectId?: string;
}

export interface MicroflowContinueEvent extends MicroflowObjectBase {
  kind: "continueEvent";
  officialType: "Microflows$ContinueEvent";
  targetLoopObjectId?: string;
}

export interface MicroflowParameterMapping {
  targetParameterId?: string;
  targetParameterName?: string;
  parameterName: string;
  parameterType?: MicroflowDataType;
  targetType?: MicroflowDataType;
  argumentExpression: MicroflowExpression;
  expression?: MicroflowExpression;
  sourceVariableName?: string;
  sourceVariableId?: string;
}

export interface MicroflowExclusiveSplit extends MicroflowObjectBase {
  kind: "exclusiveSplit";
  officialType: "Microflows$ExclusiveSplit";
  splitCondition:
    | {
        kind: "expression";
        expression: MicroflowExpression;
        resultType: "boolean" | "enumeration";
        enumerationQualifiedName?: string;
      }
    | {
        kind: "rule";
        ruleQualifiedName: string;
        parameterMappings: MicroflowParameterMapping[];
        resultType: "boolean";
      };
  errorHandlingType: MicroflowErrorHandlingType;
}

export interface MicroflowInheritanceSplit extends MicroflowObjectBase {
  kind: "inheritanceSplit";
  officialType: "Microflows$InheritanceSplit";
  inputObjectVariableName: string;
  generalizedEntityQualifiedName: string;
  allowedSpecializations: string[];
  entity: {
    generalizedEntityQualifiedName: string;
    allowedSpecializations: string[];
  };
  errorHandlingType: MicroflowErrorHandlingType;
}

export interface MicroflowExclusiveMerge extends MicroflowObjectBase {
  kind: "exclusiveMerge";
  officialType: "Microflows$ExclusiveMerge";
  mergeBehavior: {
    strategy: "firstArrived";
  };
}

export type MicroflowActionKind =
  | "retrieve"
  | "createObject"
  | "changeMembers"
  | "commit"
  | "delete"
  | "rollback"
  | "cast"
  | "aggregateList"
  | "createList"
  | "changeList"
  | "listOperation"
  | "callMicroflow"
  | "callJavaAction"
  | "callJavaScriptAction"
  | "callNanoflow"
  | "createVariable"
  | "changeVariable"
  | "closePage"
  | "downloadFile"
  | "showHomePage"
  | "showMessage"
  | "showPage"
  | "validationFeedback"
  | "synchronize"
  | "restCall"
  | "webServiceCall"
  | "importXml"
  | "exportXml"
  | "callExternalAction"
  | "restOperationCall"
  | "logMessage"
  | "generateDocument"
  | "counter"
  | "incrementCounter"
  | "gauge"
  | "mlModelCall"
  | "applyJumpToOption"
  | "callWorkflow"
  | "changeWorkflowState"
  | "completeUserTask"
  | "generateJumpToOptions"
  | "retrieveWorkflowActivityRecords"
  | "retrieveWorkflowContext"
  | "retrieveWorkflows"
  | "showUserTaskPage"
  | "showWorkflowAdminPage"
  | "lockWorkflow"
  | "unlockWorkflow"
  | "notifyWorkflow"
  | "deleteExternalObject"
  | "sendExternalObject"
  | "throwException"
  | "filterList"
  | "sortList";

export type MicroflowActionCategory = MicroflowActivityCategory;

export interface MicroflowActionInputSpec {
  id: string;
  title: string;
  dataType?: MicroflowDataType;
  required?: boolean;
}

export interface MicroflowActionOutputSpec {
  id: string;
  name: string;
  dataType: MicroflowDataType;
  source: string;
}

export interface MicroflowActionBase {
  id: string;
  officialType: string;
  kind: MicroflowActionKind;
  caption?: string;
  errorHandlingType: MicroflowErrorHandlingType;
  documentation?: string;
  inputs?: MicroflowActionInputSpec[];
  outputs?: MicroflowActionOutputSpec[];
  editor: {
    category: MicroflowActionCategory;
    iconKey: string;
    availability: MicroflowNodeAvailability;
    availabilityReason?: string;
  };
}

export interface MicroflowAssociationRetrieveSource {
  kind: "association";
  officialType: "Microflows$AssociationRetrieveSource";
  associationQualifiedName: string | null;
  startVariableName: string;
}

export interface MicroflowDatabaseRetrieveSource {
  kind: "database";
  officialType: "Microflows$DatabaseRetrieveSource";
  entityQualifiedName: string | null;
  xPathConstraint?: MicroflowExpression | null;
  sortItemList: MicroflowSortItemList;
  range: MicroflowRetrieveRange;
}

export interface MicroflowSortItemList {
  items: MicroflowSortItem[];
}

export interface MicroflowSortItem {
  attributeQualifiedName: string;
  direction: "asc" | "desc";
}

export type MicroflowRetrieveRange =
  | { kind: "all"; officialType: "Microflows$ConstantRange"; value: "all" }
  | { kind: "first"; officialType: "Microflows$ConstantRange"; value: "first" }
  | { kind: "custom"; officialType: "Microflows$CustomRange"; limitExpression: MicroflowExpression; offsetExpression?: MicroflowExpression };

export interface MicroflowRetrieveAction extends MicroflowActionBase {
  kind: "retrieve";
  officialType: "Microflows$RetrieveAction";
  outputVariableName: string;
  retrieveSource: MicroflowAssociationRetrieveSource | MicroflowDatabaseRetrieveSource;
}

export interface MicroflowMemberChange {
  id: string;
  memberQualifiedName: string;
  memberKind: "attribute" | "associationReference" | "associationReferenceSet";
  /** 当 assignmentKind 为 `clear` 时可为空。 */
  valueExpression?: MicroflowExpression;
  assignmentKind: "set" | "add" | "remove" | "clear";
}

export interface MicroflowCreateObjectAction extends MicroflowActionBase {
  kind: "createObject";
  officialType: "Microflows$CreateObjectAction";
  entityQualifiedName: string;
  outputVariableName: string;
  memberChanges: MicroflowMemberChange[];
  commit: {
    enabled: boolean;
    withEvents: boolean;
    refreshInClient: boolean;
  };
}

export interface MicroflowChangeMembersAction extends MicroflowActionBase {
  kind: "changeMembers";
  officialType: "Microflows$ChangeMembersAction";
  changeVariableName: string;
  memberChanges: MicroflowMemberChange[];
  commit: {
    enabled: boolean;
    withEvents: boolean;
    refreshInClient: boolean;
  };
  validateObject: boolean;
}

export interface MicroflowCommitAction extends MicroflowActionBase {
  kind: "commit";
  officialType: "Microflows$CommitAction";
  objectOrListVariableName: string;
  withEvents: boolean;
  refreshInClient: boolean;
}

export interface MicroflowDeleteAction extends MicroflowActionBase {
  kind: "delete";
  officialType: "Microflows$DeleteAction";
  objectOrListVariableName: string;
  withEvents: boolean;
  deleteBehavior: "deleteOnly" | "deleteAndRefreshClient";
}

export interface MicroflowRollbackAction extends MicroflowActionBase {
  kind: "rollback";
  officialType: "Microflows$RollbackAction";
  objectOrListVariableName: string;
  refreshInClient: boolean;
  rollbackMode?: "objectOnly" | "objectAndAssociations";
  failIfNotChanged?: boolean;
  clearValidationErrors?: boolean;
}

export interface MicroflowCallMicroflowAction extends MicroflowActionBase {
  kind: "callMicroflow";
  officialType: "Microflows$MicroflowCallAction";
  targetMicroflowId: string;
  targetMicroflowName?: string;
  targetMicroflowDisplayName?: string;
  targetMicroflowQualifiedName?: string;
  targetModuleId?: string;
  targetVersion?: string;
  targetSchemaId?: string;
  parameterMappings: MicroflowParameterMapping[];
  returnValue: {
    storeResult: boolean;
    outputVariableId?: string;
    outputVariableName?: string;
    resultVariableName?: string;
    dataType?: MicroflowDataType;
  };
  callMode: "sync" | "asyncReserved";
}

export interface MicroflowCreateVariableAction extends MicroflowActionBase {
  kind: "createVariable";
  officialType: "Microflows$CreateVariableAction";
  variableName: string;
  dataType: MicroflowDataType;
  initialValue?: MicroflowExpression;
  readonly: boolean;
}

export interface MicroflowChangeVariableAction extends MicroflowActionBase {
  kind: "changeVariable";
  officialType: "Microflows$ChangeVariableAction";
  targetVariableName: string;
  newValueExpression: MicroflowExpression;
}

export type MicroflowChangeListOperation = "add" | "addAll" | "addRange" | "remove" | "removeAll" | "removeWhere" | "clear" | "set";
export type MicroflowAggregateListFunction = "count" | "sum" | "average" | "min" | "max";
export type MicroflowListOperationKind =
  | "union"
  | "intersect"
  | "subtract"
  | "contains"
  | "equals"
  | "isEmpty"
  | "head"
  | "tail"
  | "find"
  | "first"
  | "last"
  | "distinct"
  | "reverse"
  | "size"
  | "filter"
  | "sort"
  | "map"
  | "take"
  | "skip";

export interface MicroflowCreateListAction extends MicroflowActionBase {
  kind: "createList";
  officialType: "Microflows$CreateListAction";
  outputListVariableName: string;
  listVariableId?: string;
  listVariableName?: string;
  elementType: MicroflowDataType;
  itemType?: MicroflowDataType;
  listType: "mutable" | "readonly";
  entityQualifiedName?: string;
  initialItemsExpression?: MicroflowExpression;
  description?: string;
}

export interface MicroflowChangeListAction extends MicroflowActionBase {
  kind: "changeList";
  officialType: "Microflows$ChangeListAction";
  targetListVariableName: string;
  targetListVariableId?: string;
  operation: MicroflowChangeListOperation;
  itemExpression?: MicroflowExpression;
  itemsExpression?: MicroflowExpression;
  sourceListVariable?: string;
  sourceListVariableName?: string;
  allowDuplicates?: boolean;
  mutateInPlace?: boolean;
  conditionExpression?: MicroflowExpression;
  indexExpression?: MicroflowExpression;
}

export interface MicroflowAggregateListAction extends MicroflowActionBase {
  kind: "aggregateList";
  officialType: "Microflows$AggregateListAction";
  listVariableName: string;
  sourceListVariableName?: string;
  sourceListVariableId?: string;
  aggregateFunction: MicroflowAggregateListFunction;
  attributeQualifiedName?: string;
  member?: string;
  aggregateExpression?: MicroflowExpression;
  outputVariableName: string;
  resultVariableId?: string;
  resultVariableName?: string;
  resultType?: MicroflowDataType;
  emptyListBehavior?: "zero" | "null" | "error";
}

export interface MicroflowListOperationAction extends MicroflowActionBase {
  kind: "listOperation";
  officialType: "Microflows$ListOperationAction";
  leftListVariableName: string;
  sourceListVariableName?: string;
  sourceListVariableId?: string;
  operation: MicroflowListOperationKind;
  rightListVariableName?: string;
  secondListVariable?: string;
  secondListVariableName?: string;
  targetListVariableName?: string;
  targetListVariableId?: string;
  objectVariableName?: string;
  itemVariable?: string;
  itemVariableName?: string;
  expression?: MicroflowExpression;
  filterExpression?: MicroflowExpression;
  sortExpression?: MicroflowExpression;
  sortKeys?: Array<{
    memberName?: string;
    expression?: MicroflowExpression;
    direction: "asc" | "desc";
    nulls?: "first" | "last";
  }>;
  limit?: number;
  offset?: number;
  distinct?: boolean;
  outputVariableName: string;
  outputListVariableName?: string;
  outputElementType?: MicroflowDataType;
}

export interface MicroflowRestCallAction extends MicroflowActionBase {
  kind: "restCall";
  officialType: "Microflows$RestCallAction";
  request: {
    method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
    urlExpression: MicroflowExpression;
    headers: MicroflowHttpHeader[];
    queryParameters: MicroflowHttpQueryParameter[];
    body:
      | { kind: "none" }
      | { kind: "json"; expression: MicroflowExpression }
      | { kind: "text"; expression: MicroflowExpression }
      | { kind: "form"; fields: MicroflowFormField[] }
      | { kind: "mapping"; exportMappingQualifiedName: string };
  };
  response: {
    handling:
      | { kind: "ignore" }
      | { kind: "string"; outputVariableName: string }
      | { kind: "json"; outputVariableName: string }
      | { kind: "importMapping"; importMappingQualifiedName: string; outputVariableName: string };
    statusCodeVariableName?: string;
    headersVariableName?: string;
  };
  timeoutSeconds: number;
}

export interface MicroflowHttpHeader {
  id: string;
  key: string;
  valueExpression: MicroflowExpression;
}

export interface MicroflowHttpQueryParameter {
  id: string;
  key: string;
  valueExpression: MicroflowExpression;
}

export interface MicroflowFormField {
  id: string;
  key: string;
  valueExpression: MicroflowExpression;
}

export interface MicroflowLogMessageAction extends MicroflowActionBase {
  kind: "logMessage";
  officialType: "Microflows$LogMessageAction";
  level: "trace" | "debug" | "info" | "warning" | "error" | "critical";
  logNodeName: string;
  template: {
    text: string;
    arguments: MicroflowExpression[];
  };
  includeContextVariables: boolean;
  includeTraceId: boolean;
}

export interface MicroflowGenericAction extends MicroflowActionBase {
  kind: Exclude<
    MicroflowActionKind,
    | MicroflowRetrieveAction["kind"]
    | MicroflowCreateObjectAction["kind"]
    | MicroflowChangeMembersAction["kind"]
    | MicroflowCommitAction["kind"]
    | MicroflowDeleteAction["kind"]
    | MicroflowRollbackAction["kind"]
    | MicroflowCallMicroflowAction["kind"]
    | MicroflowCreateVariableAction["kind"]
    | MicroflowChangeVariableAction["kind"]
    | MicroflowCreateListAction["kind"]
    | MicroflowChangeListAction["kind"]
    | MicroflowAggregateListAction["kind"]
    | MicroflowListOperationAction["kind"]
    | MicroflowRestCallAction["kind"]
    | MicroflowLogMessageAction["kind"]
  >;
  /** P1/P2 modeledOnly；不得用于 P0 kind（类型层已排除）。 */
  [key: string]: unknown;
}

export type MicroflowAction =
  | MicroflowRetrieveAction
  | MicroflowCreateObjectAction
  | MicroflowChangeMembersAction
  | MicroflowCommitAction
  | MicroflowDeleteAction
  | MicroflowRollbackAction
  | MicroflowCallMicroflowAction
  | MicroflowCreateVariableAction
  | MicroflowChangeVariableAction
  | MicroflowCreateListAction
  | MicroflowChangeListAction
  | MicroflowAggregateListAction
  | MicroflowListOperationAction
  | MicroflowRestCallAction
  | MicroflowLogMessageAction
  | MicroflowGenericAction;

export type MicroflowActionActivityColor = "default" | "blue" | "green" | "orange" | "red" | "purple" | "gray";

export interface MicroflowActionActivity extends MicroflowObjectBase {
  kind: "actionActivity";
  officialType: "Microflows$ActionActivity";
  caption: string;
  autoGenerateCaption: boolean;
  backgroundColor: MicroflowActionActivityColor;
  disabled: boolean;
  action: MicroflowAction;
}

export interface MicroflowIterableListLoopSource {
  kind: "iterableList";
  officialType: "Microflows$IterableList";
  listVariableName: string;
  iteratorVariableName: string;
  iteratorVariableDataType?: MicroflowDataType;
  currentIndexVariableName: "$currentIndex";
}

export interface MicroflowWhileLoopCondition {
  kind: "whileCondition";
  officialType: "Microflows$WhileLoopCondition";
  expression: MicroflowExpression;
}

export interface MicroflowLoopedActivity extends MicroflowObjectBase {
  kind: "loopedActivity";
  officialType: "Microflows$LoopedActivity";
  documentation: string;
  errorHandlingType: MicroflowErrorHandlingType;
  loopSource: MicroflowIterableListLoopSource | MicroflowWhileLoopCondition;
  objectCollection: MicroflowObjectCollection;
}

export interface MicroflowParameterObject extends MicroflowObjectBase {
  kind: "parameterObject";
  officialType: "Microflows$MicroflowParameterObject";
  parameterId: string;
  parameterName?: string;
}

export interface MicroflowAnnotation extends MicroflowObjectBase {
  kind: "annotation";
  officialType: "Microflows$Annotation";
  text: string;
}

export interface MicroflowParallelGateway extends MicroflowObjectBase {
  kind: "parallelGateway";
  officialType: "Microflows$ParallelGateway";
  gatewayMode: "split" | "join" | "auto";
  branches: Array<{ id: string; name?: string }>;
  joinPolicy: "waitAll" | "waitAny";
}

export interface MicroflowInclusiveGateway extends MicroflowObjectBase {
  kind: "inclusiveGateway";
  officialType: "Microflows$InclusiveGateway";
  branches: Array<{ id: string; name?: string; conditionExpression?: MicroflowExpression }>;
  defaultBranch: string | null;
  mergePolicy: "waitAny" | "waitAll";
}

export interface MicroflowTryCatch extends MicroflowObjectBase {
  kind: "tryCatch";
  officialType: "Microflows$TryCatch";
  tryBranchKey: string;
  catchBranchKey: string;
  finallyBranchKey?: string;
  errorVariableName: string;
}

export interface MicroflowErrorHandler extends MicroflowObjectBase {
  kind: "errorHandler";
  officialType: "Microflows$ErrorHandler";
  policy: "rollback" | "continue" | "custom";
  customHandlerVariable?: string;
  continueOnError: boolean;
}

export type MicroflowObject =
  | MicroflowStartEvent
  | MicroflowEndEvent
  | MicroflowErrorEvent
  | MicroflowBreakEvent
  | MicroflowContinueEvent
  | MicroflowExclusiveSplit
  | MicroflowInheritanceSplit
  | MicroflowExclusiveMerge
  | MicroflowActionActivity
  | MicroflowLoopedActivity
  | MicroflowParameterObject
  | MicroflowAnnotation
  | MicroflowParallelGateway
  | MicroflowInclusiveGateway
  | MicroflowTryCatch
  | MicroflowErrorHandler;

export interface MicroflowObjectCollection {
  id: string;
  officialType: "Microflows$MicroflowObjectCollection";
  objects: MicroflowObject[];
  flows?: MicroflowFlow[];
}

export type MicroflowCaseValue =
  | { kind: "boolean"; officialType: "Microflows$EnumerationCase"; value: true | false; persistedValue: "true" | "false" }
  | { kind: "enumeration"; officialType: "Microflows$EnumerationCase"; enumerationQualifiedName: string; value: string }
  | { kind: "inheritance"; officialType: "Microflows$InheritanceCase"; entityQualifiedName: string }
  | { kind: "empty"; officialType: "Microflows$NoCase" }
  | { kind: "fallback"; officialType: "Microflows$NoCase" }
  | { kind: "noCase"; officialType: "Microflows$NoCase" };

export interface MicroflowSequenceFlow {
  id: string;
  stableId: string;
  kind: "sequence";
  officialType: "Microflows$SequenceFlow";
  originObjectId: string;
  destinationObjectId: string;
  originConnectionIndex: number;
  destinationConnectionIndex: number;
  caseValues: MicroflowCaseValue[];
  isErrorHandler: boolean;
  line: MicroflowLine;
  editor: {
    edgeKind: "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler";
    label?: string;
    description?: string;
    branchOrder?: number;
    colorToken?: string;
    selected?: boolean;
  };
  exposeLatestError?: boolean;
  exposeLatestSoapFault?: boolean;
  exposeLatestHttpResponse?: boolean;
  targetErrorVariableName?: string;
  logError?: boolean;
}

export interface MicroflowAnnotationFlow {
  id: string;
  stableId: string;
  kind: "annotation";
  officialType: "Microflows$AnnotationFlow";
  originObjectId: string;
  destinationObjectId: string;
  originConnectionIndex?: number;
  destinationConnectionIndex?: number;
  line: MicroflowLine;
  caseValues?: MicroflowCaseValue[];
  isErrorHandler?: false;
  editor: {
    edgeKind?: "annotation";
    label?: string;
    description?: string;
    showInExport: boolean;
    selected?: boolean;
  };
  attachmentMode?: "node" | "edge" | "canvas";
}

export type MicroflowFlow = MicroflowSequenceFlow | MicroflowAnnotationFlow;

export interface MicroflowSecurityConfig {
  applyEntityAccess: boolean;
  allowedModuleRoleIds: string[];
  allowedRoleNames?: string[];
}

export interface MicroflowConcurrencyConfig {
  allowConcurrentExecution: boolean;
  errorMessage?: string;
  errorMicroflowId?: string | null;
}

export interface MicroflowExposureConfig {
  exportLevel: "hidden" | "module" | "public";
  markAsUsed: boolean;
  asMicroflowAction?: {
    enabled: boolean;
    caption?: string;
    category?: string;
  };
  asWorkflowAction?: {
    enabled: boolean;
    caption?: string;
    category?: string;
  };
  url?: {
    enabled: boolean;
    path?: string;
    searchParameters?: string[];
  };
}

export type MicroflowVariableVisibility = "definite" | "maybe" | "unavailable";

export type MicroflowVariableKind =
  | "parameter"
  | "actionOutput"
  | "localVariable"
  | "objectOutput"
  | "listOutput"
  | "primitiveOutput"
  | "microflowReturn"
  | "restResponse"
  | "loopIterator"
  | "system"
  | "errorContext"
  | "soapFault"
  | "modeledOnly"
  | "unknown";

export type MicroflowVariableSource =
  | { kind: "parameter"; parameterId: string }
  | { kind: "actionOutput"; objectId: string; actionId: string; actionKind?: MicroflowActionKind }
  | { kind: "createVariable"; objectId: string; actionId: string }
  | { kind: "createList"; objectId: string; actionId: string }
  | { kind: "aggregateList"; objectId: string; actionId: string }
  | { kind: "listOperation"; objectId: string; actionId: string }
  | { kind: "localVariable"; objectId: string; actionId: string }
  | { kind: "loopIterator"; loopObjectId: string }
  | { kind: "system"; name: "$currentUser" | "$currentIndex" }
  | { kind: "errorContext"; flowId: string; sourceObjectId?: string; errorVariable?: "$latestError" | "$latestHttpResponse" | "$latestSoapFault" }
  | { kind: "microflowReturn"; objectId: string; targetMicroflowId: string }
  | { kind: "restResponse"; objectId: string; responseKind: "string" | "json" | "importMapping" | "statusCode" | "headers" }
  | { kind: "modeledOnly"; objectId: string; actionId?: string; actionKind?: MicroflowActionKind }
  | { kind: "unknown"; objectId?: string; actionId?: string; reason?: string };

export interface MicroflowVariableScope {
  kind?: "global" | "objectCollection" | "downstream" | "branch" | "loop" | "errorHandler" | "system" | "collection";
  collectionId: string;
  startObjectId?: string;
  endObjectId?: string;
  loopObjectId?: string;
  errorHandlerFlowId?: string;
  branchSourceObjectId?: string;
  branchFlowId?: string;
}

export interface MicroflowVariableDiagnostic {
  id: string;
  severity: "error" | "warning" | "info";
  code: string;
  message: string;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  fieldPath?: string;
  variableName?: string;
}

export interface MicroflowVariableSymbol {
  id?: string;
  name: string;
  displayName?: string;
  kind?: MicroflowVariableKind;
  dataType: MicroflowDataType;
  type?: MicroflowTypeRef;
  source: MicroflowVariableSource;
  scope: MicroflowVariableScope;
  visibility?: MicroflowVariableVisibility;
  readonly: boolean;
  availableFromObjectId?: string;
  availableInCollectionId?: string;
  availableInObjectIds?: string[];
  branchSourceObjectId?: string;
  branchFlowId?: string;
  loopObjectId?: string;
  errorHandlerFlowId?: string;
  maybeReason?: string;
  unavailableReason?: string;
  diagnostics?: MicroflowVariableDiagnostic[];
  documentation?: string;
}

export interface MicroflowVariableGraphAnalysis {
  collectionIds: string[];
  objectIds: string[];
  normalEdges: Array<{ flowId: string; fromObjectId: string; toObjectId: string; collectionId: string; edgeKind: "sequence" | "decisionCondition" | "objectTypeCondition" | "loopEntry" }>;
  errorHandlerEdges: Array<{ flowId: string; fromObjectId: string; toObjectId: string; collectionId: string }>;
  annotationFlowIds: string[];
  startObjectIdsByCollection: Record<string, string[]>;
}

export interface MicroflowVariableIndex {
  schemaId?: string;
  builtAt?: string;
  metadataVersion?: string;
  all?: MicroflowVariableSymbol[];
  byName?: Record<string, MicroflowVariableSymbol[]>;
  byObjectId?: Record<string, MicroflowVariableSymbol[]>;
  byActionId?: Record<string, MicroflowVariableSymbol[]>;
  byCollectionId?: Record<string, MicroflowVariableSymbol[]>;
  byScopeKey?: Record<string, MicroflowVariableSymbol[]>;
  diagnostics?: MicroflowVariableDiagnostic[];
  graphAnalysis?: MicroflowVariableGraphAnalysis;
  parameters: Record<string, MicroflowVariableSymbol>;
  localVariables: Record<string, MicroflowVariableSymbol>;
  objectOutputs: Record<string, MicroflowVariableSymbol>;
  listOutputs: Record<string, MicroflowVariableSymbol>;
  loopVariables: Record<string, MicroflowVariableSymbol>;
  errorVariables: Record<string, MicroflowVariableSymbol>;
  systemVariables: Record<string, MicroflowVariableSymbol>;
}

export interface MicroflowValidationState {
  lastValidatedAt?: string;
  issues: MicroflowValidationIssue[];
}

export type MicroflowValidationSeverity = "error" | "warning" | "info";
export type MicroflowValidationSource =
  | "schema"
  | "document"
  | "node"
  | "flow"
  | "parameter"
  | "variable"
  | "expression"
  | "decision"
  | "callMicroflow"
  | "loop"
  | "list"
  | "object"
  | "metadata"
  | "reference"
  | "server"
  | "domainModel"
  | "root"
  | "objectCollection"
  | "event"
  | "action"
  | "errorHandling"
  | "reachability";

export interface MicroflowValidator {
  validate(schema: MicroflowSchema): MicroflowValidationIssue[];
}

export interface MicroflowEditorState {
  viewport: {
    x: number;
    y: number;
    zoom: number;
  };
  zoom: number;
  selectedObjectId?: string;
  selectedFlowId?: string;
  selectedCollectionId?: string;
  activeBottomPanel?: "problems" | "debug" | "variables" | "none";
  leftPanelCollapsed?: boolean;
  rightPanelCollapsed?: boolean;
  bottomPanelCollapsed?: boolean;
  showMiniMap?: boolean;
  gridEnabled?: boolean;
  selection: {
    objectId?: string;
    flowId?: string;
    collectionId?: string;
  };
  layoutMode?: "freeform" | "auto";
}

export interface MicroflowAuditState {
  createdBy?: string;
  createdAt?: string;
  updatedBy?: string;
  updatedAt?: string;
  version: string;
  status: "draft" | "published" | "archived";
}

/**
 * @deprecated Legacy demo persistence shape (nodes/edges). Use {@link MicroflowAuthoringSchema} only for resources/API.
 */
export interface LegacyMicroflowGraphSchema {
  id: string;
  name: string;
  version: string;
  description?: string;
  parameters: MicroflowParameter[];
  variables: MicroflowVariable[];
  nodes: LegacyMicroflowNode[];
  edges: LegacyMicroflowEdge[];
  viewport?: {
    zoom: number;
    offset: MicroflowPosition;
  };
}

export interface MicroflowAuthoringSchema {
  schemaVersion: string;
  mendixProfile: "mx10" | "mx11";
  id: string;
  stableId: string;
  name: string;
  displayName: string;
  description?: string;
  documentation?: string;
  moduleId: string;
  moduleName?: string;
  parameters: MicroflowParameter[];
  returnType: MicroflowDataType;
  returnVariableName?: string;
  objectCollection: MicroflowObjectCollection;
  flows: MicroflowFlow[];
  security: MicroflowSecurityConfig;
  concurrency: MicroflowConcurrencyConfig;
  exposure: MicroflowExposureConfig;
  variables?: MicroflowVariableIndex;
  validation: MicroflowValidationState;
  debug?: MicroflowDebugState;
  editor: MicroflowEditorState;
  audit: MicroflowAuditState;
}

/** Persisted on authoring JSON (`debug.traceFrames` / `lastTrace`). Runtime execution frames use `MicroflowTraceFrame` in `debug/trace-types`. */
export interface MicroflowAuthoringPersistedTraceFrame {
  id?: string;
  frameId?: string;
  runId?: string;
  objectId: string;
  nodeId?: string;
  objectTitle?: string;
  nodeTitle?: string;
  incomingFlowId?: string;
  outgoingFlowId?: string;
  incomingEdgeId?: string;
  outgoingEdgeId?: string;
  selectedCaseValue?: MicroflowCaseValue;
  status: "idle" | "running" | "success" | "failed" | "unsupported" | "skipped" | "completed";
  startedAt?: string;
  durationMs: number;
  input?: Record<string, unknown>;
  output?: Record<string, unknown>;
  error?: string | { code: string; message: string; objectId?: string; nodeId?: string; actionId?: string; flowId?: string; details?: unknown };
  variablesSnapshot?: Record<string, unknown>;
}

export interface MicroflowDebugState {
  traceFrames: MicroflowAuthoringPersistedTraceFrame[];
  lastTrace?: MicroflowAuthoringPersistedTraceFrame[];
  activeFrameIndex?: number;
}

export interface MicroflowEditorPort {
  id: string;
  objectId: string;
  label: string;
  direction: MicroflowPortDirection;
  kind: MicroflowPortKind;
  connectionIndex: number;
  cardinality?: MicroflowPortCardinality;
  position?: MicroflowPoint;
  edgeTypes: MicroflowEdgeKind[];
}

export interface MicroflowEditorNode {
  id: string;
  objectId: string;
  kind?: MicroflowObjectKind;
  nodeKind: MicroflowObjectKind;
  activityKind?: MicroflowActionKind;
  title: string;
  subtitle?: string;
  iconKey: string;
  position: MicroflowPoint;
  size: MicroflowSize;
  ports: MicroflowEditorPort[];
  parentObjectId?: string;
  collectionId: string;
  state: {
    selected: boolean;
    disabled: boolean;
    hasError: boolean;
    hasWarning: boolean;
    runtimeStatus?: "idle" | "running" | "success" | "failed" | "skipped";
  };
}

export interface MicroflowEditorEdge {
  id: string;
  flowId: string;
  kind?: MicroflowEdgeKind;
  sourceNodeId: string;
  sourceObjectId?: string;
  targetNodeId: string;
  targetObjectId?: string;
  sourcePortId: string;
  targetPortId: string;
  edgeKind: MicroflowEdgeKind;
  label?: string;
  style: {
    strokeType: "solid" | "dashed" | "dotted";
    colorToken: string;
    arrow: boolean;
  };
  state: {
    selected: boolean;
    hasError: boolean;
    runtimeVisited: boolean;
  };
}

export interface MicroflowEditorGraph {
  nodes: MicroflowEditorNode[];
  edges: MicroflowEditorEdge[];
  viewport: {
    x: number;
    y: number;
    zoom: number;
  };
  selection: {
    objectId?: string;
    flowId?: string;
  };
}

export interface MicroflowEditorGraphPatch {
  movedNodes?: Array<{ objectId: string; position: MicroflowPoint }>;
  resizedNodes?: Array<{ objectId: string; size: MicroflowSize }>;
  selectedObjectId?: string;
  selectedFlowId?: string;
  selectedCollectionId?: string;
  viewport?: MicroflowEditorGraph["viewport"];
  updatedFlows?: Array<{ flowId: string; label?: string; line?: MicroflowLine }>;
  addObject?: { object: MicroflowObject; parentLoopObjectId?: string };
  updateObject?: { objectId: string; patch: Partial<MicroflowObject> };
  deleteObjectId?: string;
  addFlow?: MicroflowFlow;
  updateFlow?: { flowId: string; patch: Partial<MicroflowFlow> };
  deleteFlowId?: string;
}

export interface MendixCompatText {
  translations?: Record<string, string>;
  text?: string;
}

export type MendixCompatPrimitiveDataType =
  | "Boolean"
  | "Integer"
  | "Long"
  | "Decimal"
  | "String"
  | "DateTime"
  | "Binary"
  | "Json"
  | "Void"
  | "Unknown";

export type MendixCompatDataType =
  | { $Type: "DataTypes$PrimitiveType"; primitive?: MendixCompatPrimitiveDataType }
  | { $Type: "DataTypes$EnumerationType"; enumerationQualifiedName: string }
  | { $Type: "DataTypes$ObjectType"; entityQualifiedName: string }
  | { $Type: "DataTypes$ListType"; itemType: MendixCompatDataType }
  | { $Type: "DataTypes$FileDocumentType"; entityQualifiedName?: string }
  | { $Type: "DataTypes$MicroflowDataType"; authoringType: MicroflowDataType };
export type MendixCompatMicroflowParameter = MicroflowParameter;

export interface MendixCompatObjectBase {
  $ID: string;
  $Type: MicroflowObject["officialType"];
  stableId?: string;
  caption?: string;
  documentation?: string;
  relativeMiddlePoint: MicroflowPoint;
  size: MicroflowSize;
  disabled?: boolean;
  authoringKind: MicroflowObjectKind;
}

export interface MendixCompatAction {
  $ID: string;
  $Type: MicroflowAction["officialType"];
  kind: MicroflowActionKind;
  errorHandlingType: MicroflowErrorHandlingType;
  category: MicroflowActionCategory;
  authoringAction: MicroflowAction;
}

export interface MendixCompatObject extends MendixCompatObjectBase {
  action?: MendixCompatAction;
  objectCollection?: MendixCompatMicroflowObjectCollection;
  parameterId?: string;
  splitCondition?: MicroflowExclusiveSplit["splitCondition"];
  flowBehavior?: MicroflowExclusiveMerge["mergeBehavior"] | MicroflowLoopedActivity["loopSource"];
  authoringObject: MicroflowObject;
}

export interface MendixCompatMicroflowObjectCollection {
  $ID: string;
  $Type: "Microflows$MicroflowObjectCollection";
  objects: MendixCompatObject[];
  authoringCollection: MicroflowObjectCollection;
}

export interface MendixCompatFlowBase {
  $ID: string;
  $Type: "Microflows$SequenceFlow" | "Microflows$AnnotationFlow";
  stableId?: string;
  originObjectId: string;
  destinationObjectId: string;
  line: MicroflowLine;
}

export interface MendixCompatSequenceFlow extends MendixCompatFlowBase {
  $Type: "Microflows$SequenceFlow";
  caseValues: MicroflowCaseValue[];
  isErrorHandler: boolean;
  edgeKind: MicroflowSequenceFlow["editor"]["edgeKind"];
  authoringFlow: MicroflowSequenceFlow;
}

export interface MendixCompatAnnotationFlow extends MendixCompatFlowBase {
  $Type: "Microflows$AnnotationFlow";
  showInExport: boolean;
  authoringFlow: MicroflowAnnotationFlow;
}

export type MendixCompatFlow = MendixCompatSequenceFlow | MendixCompatAnnotationFlow;

export interface MendixCompatMicroflowActionInfo {
  caption?: string;
  category?: string;
}

export interface MendixCompatMicroflow {
  $ID: string;
  $Type: "Microflows$Microflow";
  $UnitID?: string;
  name: string;
  documentation: string;
  parameters: MendixCompatMicroflowParameter[];
  microflowReturnType: MendixCompatDataType;
  returnVariableName: string;
  objectCollection: MicroflowObjectCollection;
  flows: MicroflowFlow[];
  applyEntityAccess: boolean;
  allowedModuleRoleIds: string[];
  allowConcurrentExecution: boolean;
  concurrencyErrorMessage?: MendixCompatText;
  concurrencyErrorMicroflow?: string | null;
  excluded: boolean;
  exportLevel: "Hidden" | "UsableFromModule" | "Public";
  markAsUsed: boolean;
  microflowActionInfo?: MendixCompatMicroflowActionInfo | null;
  workflowActionInfo?: MendixCompatMicroflowActionInfo | null;
  url?: string;
  urlSearchParameters?: string[];
  stableId?: string;
}

/**
 * 与 P0 强类型动作一一对应的运行时 DTO 联合（无 editor/inputs/outputs 等设计器字段）。
 * @see mapAuthoringP0ToRuntimeDtos
 */
export type MicroflowDiscriminatedRuntimeP0ActionDto =
  | RuntimeRetrieveP0Dto
  | RuntimeCreateObjectP0Dto
  | RuntimeChangeMembersP0Dto
  | RuntimeCommitP0Dto
  | RuntimeDeleteP0Dto
  | RuntimeRollbackP0Dto
  | RuntimeCreateVariableP0Dto
  | RuntimeChangeVariableP0Dto
  | RuntimeCallMicroflowP0Dto
  | RuntimeRestCallP0Dto
  | RuntimeLogMessageP0Dto;

export type MicroflowRuntimeP0Block =
  | { objectId: string; supportLevel: "supported"; action: MicroflowDiscriminatedRuntimeP0ActionDto }
  | { objectId: string; supportLevel: "error"; code: "MF_P0_MALFORMED"; actionKind: string; message: string };

type RuntimeP0Base = { actionId: string; officialType: string; supportLevel: "supported" };

export type RuntimeRetrieveP0Dto = RuntimeP0Base & {
  actionKind: "retrieve";
  errorHandlingType: MicroflowErrorHandlingType;
  config: {
    outputVariableName: string;
    retrieveSource: MicroflowAssociationRetrieveSource | MicroflowDatabaseRetrieveSource;
  };
};

export type RuntimeCreateObjectP0Dto = RuntimeP0Base & {
  actionKind: "createObject";
  errorHandlingType: MicroflowErrorHandlingType;
  config: {
    entityQualifiedName: string;
    outputVariableName: string;
    memberChanges: MicroflowMemberChange[];
    commit: MicroflowCreateObjectAction["commit"];
  };
};

export type RuntimeChangeMembersP0Dto = RuntimeP0Base & {
  actionKind: "changeMembers";
  errorHandlingType: MicroflowErrorHandlingType;
  config: {
    changeVariableName: string;
    memberChanges: MicroflowMemberChange[];
    commit: MicroflowChangeMembersAction["commit"];
    validateObject: boolean;
  };
};

export type RuntimeCommitP0Dto = RuntimeP0Base & {
  actionKind: "commit";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { objectOrListVariableName: string; withEvents: boolean; refreshInClient: boolean };
};

export type RuntimeDeleteP0Dto = RuntimeP0Base & {
  actionKind: "delete";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { objectOrListVariableName: string; withEvents: boolean; deleteBehavior: "deleteOnly" | "deleteAndRefreshClient" };
};

export type RuntimeRollbackP0Dto = RuntimeP0Base & {
  actionKind: "rollback";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { objectOrListVariableName: string; refreshInClient: boolean };
};

export type RuntimeCreateVariableP0Dto = RuntimeP0Base & {
  actionKind: "createVariable";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { variableName: string; dataType: MicroflowDataType; initialValue?: MicroflowExpression; readonly: boolean };
};

export type RuntimeChangeVariableP0Dto = RuntimeP0Base & {
  actionKind: "changeVariable";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { targetVariableName: string; newValueExpression: MicroflowExpression };
};

export type RuntimeCallMicroflowP0Dto = RuntimeP0Base & {
  actionKind: "callMicroflow";
  errorHandlingType: MicroflowErrorHandlingType;
  config: {
    targetMicroflowId: string;
    targetMicroflowName?: string;
    targetMicroflowDisplayName?: string;
    targetMicroflowQualifiedName?: string;
    targetModuleId?: string;
    targetVersion?: string;
    targetSchemaId?: string;
    parameterMappings: MicroflowParameterMapping[];
    returnValue: MicroflowCallMicroflowAction["returnValue"];
    callMode: "sync" | "asyncReserved";
  };
};

export type RuntimeRestCallP0Dto = RuntimeP0Base & {
  actionKind: "restCall";
  errorHandlingType: MicroflowErrorHandlingType;
  config: { request: MicroflowRestCallAction["request"]; response: MicroflowRestCallAction["response"]; timeoutSeconds: number };
};

export type RuntimeLogMessageP0Dto = RuntimeP0Base & {
  actionKind: "logMessage";
  errorHandlingType: MicroflowErrorHandlingType;
  config: {
    level: MicroflowLogMessageAction["level"];
    logNodeName: string;
    template: MicroflowLogMessageAction["template"];
    includeContextVariables: boolean;
    includeTraceId: boolean;
  };
};

export interface MicroflowRuntimeDto {
  microflowId: string;
  schemaVersion: string;
  name: string;
  returnType: MicroflowDataType;
  parameters: MicroflowParameter[];
  objectCollection: MicroflowObjectCollection;
  flows: MicroflowFlow[];
  variables: MicroflowVariableIndex;
  p0RuntimeActionBlocks: MicroflowRuntimeP0Block[];
}

/**
 * @deprecated Prefer {@link MicroflowAuthoringSchema}. Kept as an alias for gradual migration of import sites.
 */
export type MicroflowSchema = MicroflowAuthoringSchema;

export interface MicroflowResource {
  id: string;
  name: string;
  description: string;
  moduleId: string;
  moduleName?: string;
  ownerName: string;
  sharedWithMe?: boolean;
  tags: string[];
  version: string;
  status: MicroflowResourceStatus;
  favorite: boolean;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
  lastModifiedBy?: string;
  schema: MicroflowAuthoringSchema;
}

export interface MicroflowListQuery {
  scope?: MicroflowResourceScope;
  keyword?: string;
  sortBy?: MicroflowResourceSortKey;
  status?: "all" | MicroflowResourceStatus;
  tag?: string;
  ownerName?: string;
  updatedRange?: "all" | "today" | "week" | "month";
}

export interface CreateMicroflowInput {
  name: string;
  description: string;
  moduleId: string;
  moduleName?: string;
  tags: string[];
  returnType?: MicroflowTypeRef;
}

export interface PublishMicroflowPayload {
  version: string;
  releaseNote: string;
  overwriteCurrent: boolean;
}

export interface MicroflowReference {
  id: string;
  sourceType: "workflow" | "agent" | "lowcode-app" | "form-event";
  sourceName: string;
  sourceId: string;
  updatedAt: string;
}

export interface MicroflowValidationIssue {
  id: string;
  microflowId?: string;
  severity: MicroflowValidationSeverity;
  message: string;
  nodeId?: string;
  edgeId?: string;
  objectId?: string;
  flowId?: string;
  actionId?: string;
  actionKind?: string;
  nodeKind?: string;
  parameterId?: string;
  collectionId?: string;
  fieldPath?: string;
  code: string;
  source?: MicroflowValidationSource;
  blockSave?: boolean;
  blockPublish?: boolean;
  createdAt?: string;
  quickFixAvailable?: boolean;
  quickFixes?: MicroflowQuickFix[];
  relatedObjectIds?: string[];
  relatedFlowIds?: string[];
  details?: string | Record<string, unknown>;
}

export interface MicroflowQuickFix {
  id: string;
  title: string;
  description?: string;
  fieldPath?: string;
  kind?: "selectObject" | "selectFlow" | "openProperty" | "setField" | "createMissingFlow" | "removeFlow" | "removeObject" | "renameVariable";
  payload?: unknown;
}

export interface MicroflowRuntimeNodeDto {
  nodeId: string;
  type: LegacyMicroflowNodeType;
  kind?: LegacyMicroflowNodeKind;
  activityType?: MicroflowActivityType;
  title: string;
  config: Record<string, unknown>;
}

export interface MicroflowRuntimeEdgeDto {
  edgeId: string;
  type: MicroflowEdgeKind;
  sourceNodeId: string;
  targetNodeId: string;
  label?: string;
  sourcePortId?: string;
  targetPortId?: string;
  conditionValue?: MicroflowConditionValue;
  errorHandlingType?: MicroflowErrorHandlingType;
  runtimeEffect?: "controlFlow" | "errorFlow" | "annotationOnly";
}
