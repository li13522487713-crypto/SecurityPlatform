export type MicroflowNodeKind =
  | "event"
  | "decision"
  | "objectTypeDecision"
  | "merge"
  | "loop"
  | "parameter"
  | "annotation"
  | "activity";

export type MicroflowNodeType =
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
  | "activity";

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
  | "sendExternalObject";

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
  id: string;
  language: MicroflowExpressionLanguage;
  text: string;
  expectedType?: MicroflowTypeRef;
  referencedVariables?: string[];
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
  name: string;
  type: MicroflowTypeRef;
  required: boolean;
  description?: string;
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

export interface MicroflowNodeBase<TKind extends MicroflowNodeType = MicroflowNodeType, TConfig extends object = Record<string, unknown>> {
  id: string;
  type: TKind;
  kind?: MicroflowNodeKind;
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

export interface MicroflowActivityConfig {
  activityType: MicroflowActivityType;
  activityCategory?: MicroflowActivityCategory;
  entity?: string;
  association?: string;
  objectVariableName?: string;
  listVariableName?: string;
  variableName?: string;
  variableType?: MicroflowTypeRef;
  valueExpression?: MicroflowExpression;
  targetMicroflowId?: string;
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url?: string;
  headers?: Array<{ key: string; value: string }>;
  query?: Array<{ key: string; value: string }>;
  bodyExpression?: MicroflowExpression;
  timeoutMs?: number;
  resultVariableName?: string;
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
  parameterMappings?: Array<{ id: string; parameterName: string; expression: MicroflowExpression }>;
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
  sourceVariableName?: string;
  targetEntity?: string;
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

export type MicroflowEventNode = MicroflowNodeBase<
  "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent",
  MicroflowEventConfig
>;
export type MicroflowDecisionNode = MicroflowNodeBase<"decision", MicroflowDecisionConfig>;
export type MicroflowObjectTypeDecisionNode = MicroflowNodeBase<"objectTypeDecision", MicroflowObjectTypeDecisionConfig>;
export type MicroflowMergeNode = MicroflowNodeBase<"merge", MicroflowMergeConfig>;
export type MicroflowLoopNode = MicroflowNodeBase<"loop", MicroflowLoopConfig>;
export type MicroflowActivityNode = MicroflowNodeBase<"activity", MicroflowActivityConfig>;
export type MicroflowParameterNode = MicroflowNodeBase<"parameter", MicroflowParameterConfig>;
export type MicroflowAnnotationNode = MicroflowNodeBase<"annotation", MicroflowAnnotationConfig>;

export type MicroflowNode =
  | MicroflowEventNode
  | MicroflowDecisionNode
  | MicroflowObjectTypeDecisionNode
  | MicroflowMergeNode
  | MicroflowLoopNode
  | MicroflowActivityNode
  | MicroflowParameterNode
  | MicroflowAnnotationNode;

export interface MicroflowEdgeBase<TKind extends MicroflowEdgeKind = MicroflowEdgeKind> {
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

export type MicroflowSequenceEdge = MicroflowEdgeBase<"sequence">;
export type MicroflowDecisionConditionEdge = MicroflowEdgeBase<"decisionCondition">;
export type MicroflowObjectTypeConditionEdge = MicroflowEdgeBase<"objectTypeCondition">;
export type MicroflowErrorHandlerEdge = MicroflowEdgeBase<"errorHandler">;
export type MicroflowAnnotationEdge = MicroflowEdgeBase<"annotation">;
export type MicroflowEdge =
  | MicroflowSequenceEdge
  | MicroflowDecisionConditionEdge
  | MicroflowObjectTypeConditionEdge
  | MicroflowErrorHandlerEdge
  | MicroflowAnnotationEdge;

export interface MicroflowSchema {
  id: string;
  name: string;
  version: string;
  description?: string;
  parameters: MicroflowParameter[];
  variables: MicroflowVariable[];
  nodes: MicroflowNode[];
  edges: MicroflowEdge[];
  viewport?: {
    zoom: number;
    offset: MicroflowPosition;
  };
}

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
  schema: MicroflowSchema;
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
  severity: "error" | "warning" | "info";
  message: string;
  nodeId?: string;
  edgeId?: string;
  code: string;
}

export interface MicroflowRuntimeNodeDto {
  nodeId: string;
  type: MicroflowNodeType;
  kind?: MicroflowNodeKind;
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
