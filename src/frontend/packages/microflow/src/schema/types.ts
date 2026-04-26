export type MicroflowNodeCategory =
  | "event"
  | "decision"
  | "activity"
  | "loop"
  | "parameter"
  | "annotation"
  | "merge";

export type MicroflowNodeKind =
  | "startEvent"
  | "endEvent"
  | "errorEvent"
  | "breakEvent"
  | "continueEvent"
  | "decision"
  | "merge"
  | "loop"
  | "parameter"
  | "annotation"
  | "activity";

export type MicroflowActivityType =
  | "objectCreate"
  | "objectChange"
  | "objectCommit"
  | "objectDelete"
  | "objectRetrieve"
  | "objectRollback"
  | "variableCreate"
  | "variableChange"
  | "callMicroflow"
  | "callRest"
  | "logMessage"
  | "showPage"
  | "closePage";

export type MicroflowEdgeType = "sequence" | "error" | "annotation";
export type MicroflowPortDirection = "input" | "output";
export type MicroflowExpressionLanguage = "mendix" | "javascript" | "plainText";
export type MicroflowErrorHandlingMode =
  | "rollback"
  | "customWithRollback"
  | "customWithoutRollback"
  | "continue";

export interface MicroflowPosition {
  x: number;
  y: number;
}

export interface MicroflowPort {
  id: string;
  label: string;
  direction: MicroflowPortDirection;
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

export interface MicroflowNodeBase<TKind extends MicroflowNodeKind = MicroflowNodeKind, TConfig extends object = Record<string, unknown>> {
  id: string;
  type: TKind;
  title: string;
  description?: string;
  category: MicroflowNodeCategory;
  position: MicroflowPosition;
  ports: MicroflowPort[];
  config: TConfig;
  render: MicroflowRenderMetadata;
  propertyForm: MicroflowPropertyFormMetadata;
  validation?: {
    disabled?: boolean;
  };
}

export interface MicroflowEventConfig {
  returnValue?: MicroflowExpression;
  errorName?: string;
}

export interface MicroflowDecisionConfig {
  expression: MicroflowExpression;
}

export interface MicroflowMergeConfig {
  strategy: "firstAvailable" | "all";
}

export interface MicroflowLoopConfig {
  iterableVariableName: string;
  itemVariableName: string;
}

export interface MicroflowActivityConfig {
  activityType: MicroflowActivityType;
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
  messageExpression?: MicroflowExpression;
  pageName?: string;
  errorHandling?: MicroflowErrorHandling;
  supportsErrorFlow?: boolean;
}

export interface MicroflowParameterConfig {
  parameter: MicroflowParameter;
}

export interface MicroflowAnnotationConfig {
  text: string;
  color?: string;
}

export type MicroflowEventNode = MicroflowNodeBase<
  "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent",
  MicroflowEventConfig
>;
export type MicroflowDecisionNode = MicroflowNodeBase<"decision", MicroflowDecisionConfig>;
export type MicroflowMergeNode = MicroflowNodeBase<"merge", MicroflowMergeConfig>;
export type MicroflowLoopNode = MicroflowNodeBase<"loop", MicroflowLoopConfig>;
export type MicroflowActivityNode = MicroflowNodeBase<"activity", MicroflowActivityConfig>;
export type MicroflowParameterNode = MicroflowNodeBase<"parameter", MicroflowParameterConfig>;
export type MicroflowAnnotationNode = MicroflowNodeBase<"annotation", MicroflowAnnotationConfig>;

export type MicroflowNode =
  | MicroflowEventNode
  | MicroflowDecisionNode
  | MicroflowMergeNode
  | MicroflowLoopNode
  | MicroflowActivityNode
  | MicroflowParameterNode
  | MicroflowAnnotationNode;

export interface MicroflowEdge {
  id: string;
  type: MicroflowEdgeType;
  sourceNodeId: string;
  sourcePortId?: string;
  targetNodeId: string;
  targetPortId?: string;
  label?: string;
  condition?: MicroflowExpression;
}

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
  type: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  title: string;
  config: Record<string, unknown>;
}

export interface MicroflowRuntimeEdgeDto {
  edgeId: string;
  type: MicroflowEdgeType;
  sourceNodeId: string;
  targetNodeId: string;
  label?: string;
}
