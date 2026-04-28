export type Ref<TKind extends string = string> = {
  kind: TKind;
  id: string;
  moduleId?: string;
  name?: string;
};

export type EntityType = "persistable" | "nonPersistable" | "external" | "view";

export type DataTypeSchema =
  | { kind: "Boolean" }
  | { kind: "String" }
  | { kind: "Integer" }
  | { kind: "Long" }
  | { kind: "Decimal"; precision?: number; scale?: number }
  | { kind: "DateTime" }
  | { kind: "Enumeration"; enumerationRef: Ref<"enumeration"> }
  | { kind: "Binary" }
  | { kind: "Object"; entityRef: Ref<"entity"> }
  | { kind: "List"; entityRef: Ref<"entity"> }
  | { kind: "Nothing" };

export type AttributeType =
  | "Boolean"
  | "String"
  | "Integer"
  | "Long"
  | "Decimal"
  | "DateTime"
  | "Enumeration"
  | "Binary"
  | "AutoNumber";

export interface AttributeSchema {
  attributeId: string;
  entityId: string;
  name: string;
  caption?: string;
  required?: boolean;
  defaultValue?: string;
  attributeType: AttributeType;
  dataType: DataTypeSchema;
}

export interface AssociationSchema {
  associationId: string;
  moduleId: string;
  name: string;
  fromEntityRef: Ref<"entity">;
  toEntityRef: Ref<"entity">;
  owner: "default" | "both";
  cardinality: "oneToOne" | "oneToMany" | "manyToMany";
}

export interface EnumerationSchema {
  enumerationId: string;
  moduleId: string;
  name: string;
  values: Array<{
    key: string;
    caption: string;
    color?: string;
  }>;
}

export interface EntityAccessRuleSchema {
  ruleId: string;
  roleRefs: Ref<"moduleRole">[];
  xpathConstraint?: string;
  memberAccess: Array<{
    attributeRef: Ref<"attribute">;
    read: boolean;
    write: boolean;
  }>;
}

export interface EntitySchema {
  entityId: string;
  moduleId: string;
  name: string;
  caption?: string;
  description?: string;
  entityType: EntityType;
  generalization?: Ref<"entity">;
  attributes: AttributeSchema[];
  associations: Ref<"association">[];
  accessRules: EntityAccessRuleSchema[];
  validationRules: Array<{ ruleId: string; expression: ExpressionSchema }>;
  eventHandlers: Array<{ event: "beforeCommit" | "afterCommit"; microflowRef: Ref<"microflow"> }>;
  systemMembers: {
    storeOwner: boolean;
    storeCreatedDate: boolean;
    storeChangedDate: boolean;
  };
  ui?: {
    x?: number;
    y?: number;
    width?: number;
    height?: number;
    color?: string;
  };
}

export interface DomainModelSchema {
  entities: EntitySchema[];
  associations: AssociationSchema[];
  enumerations: EnumerationSchema[];
}

export type PageType = "responsive" | "popup" | "layout" | "snippet";

export interface DataSourceSchema {
  sourceType: "entity" | "microflow" | "association" | "parameter" | "static";
  entityRef?: Ref<"entity">;
  microflowRef?: Ref<"microflow">;
  associationRef?: Ref<"association">;
  parameterName?: string;
}

export interface WidgetBindingSchema {
  bindingType: "value" | "items" | "entity";
  source: "attribute" | "expression" | "parameter" | "static";
  attributeRef?: Ref<"attribute">;
  expression?: ExpressionSchema;
  parameterName?: string;
  staticValue?: string | number | boolean | null;
}

export interface ConditionalRuleSchema {
  expression: ExpressionSchema;
}

type WidgetBase = {
  widgetId: string;
  widgetType: WidgetType;
  props: Record<string, unknown>;
  bindings?: WidgetBindingSchema[];
  children?: WidgetSchema[];
  slots?: Record<string, WidgetSchema[]>;
  events?: WidgetEventSchema[];
  visibility?: ConditionalRuleSchema;
  editability?: ConditionalRuleSchema;
  style?: Record<string, unknown>;
  layout?: Record<string, unknown>;
};

export type WidgetType =
  | "container"
  | "dataView"
  | "textBox"
  | "textArea"
  | "numberInput"
  | "dropDown"
  | "button"
  | "dataGrid"
  | "listView"
  | "label";

export type WidgetSchema =
  | (WidgetBase & { widgetType: "container" })
  | (WidgetBase & {
      widgetType: "dataView";
      dataSource: DataSourceSchema;
    })
  | (WidgetBase & {
      widgetType: "textBox" | "textArea" | "numberInput" | "dropDown";
      fieldBinding?: WidgetBindingSchema;
    })
  | (WidgetBase & { widgetType: "label" })
  | (WidgetBase & {
      widgetType: "button";
      action?: ExecuteActionRequest;
    })
  | (WidgetBase & { widgetType: "dataGrid" | "listView"; dataSource?: DataSourceSchema });

export interface WidgetEventSchema {
  eventName: "onClick" | "onChange" | "onLoad";
  action: ExecuteActionRequest;
}

export interface PageParameterSchema {
  name: string;
  type: DataTypeSchema;
}

export interface NavigationItemSchema {
  itemId: string;
  caption: string;
  pageRef: Ref<"page">;
}

export interface PageSchema {
  pageId: string;
  moduleId: string;
  name: string;
  pageType: PageType;
  layoutRef?: Ref<"page">;
  parameters: PageParameterSchema[];
  rootWidget: WidgetSchema;
  allowedRoles: Ref<"moduleRole">[];
}

export type ExpressionNode =
  | { type: "literal"; value: string | number | boolean | null }
  | { type: "variable"; name: string }
  | { type: "path"; root: string; segments: string[] }
  | {
      type: "binary";
      operator: "and" | "or" | "=" | "!=" | ">" | "<" | ">=" | "<=";
      left: ExpressionNode;
      right: ExpressionNode;
    }
  | {
      type: "function";
      functionName: "empty" | "contains";
      args: ExpressionNode[];
    }
  | {
      type: "if";
      condition: ExpressionNode;
      thenNode: ExpressionNode;
      elseNode: ExpressionNode;
    }
  | { type: "enum"; enumerationName: string; value: string };

export interface ExpressionSchema {
  source: string;
  ast: ExpressionNode;
  returnType?: DataTypeSchema;
  dependencies: Array<Ref<"attribute" | "variable" | "entity" | "enumeration">>;
  validation: Array<{ code: string; message: string; severity: "error" | "warning" | "info" }>;
}

export interface MicroflowParameterSchema {
  name: string;
  type: DataTypeSchema;
}

export type MicroflowNodeType =
  | "startEvent"
  | "endEvent"
  | "decision"
  | "retrieveObject"
  | "changeObject"
  | "commitObject"
  | "createVariable"
  | "changeVariable"
  | "showMessage"
  | "validationFeedback"
  | "callWorkflow"
  | "callMicroflow";

type MicroflowNodeBase = {
  nodeId: string;
  type: MicroflowNodeType;
  caption: string;
  position: { x: number; y: number };
  input?: string[];
  output?: string[];
  errorHandling?: { strategy: "abort" | "custom"; microflowRef?: Ref<"microflow"> };
};

export type MicroflowNodeSchema =
  | (MicroflowNodeBase & { type: "startEvent" })
  | (MicroflowNodeBase & {
      type: "endEvent";
      returnExpression?: ExpressionSchema;
    })
  | (MicroflowNodeBase & {
      type: "decision";
      expression: ExpressionSchema;
      outcomes?: Array<{ key: string; caption: string }>;
    })
  | (MicroflowNodeBase & {
      type: "retrieveObject";
      retrieveSource: "database" | "association";
      entityRef?: Ref<"entity">;
      xpath?: string;
      range?: { first?: number; amount?: number };
      sort?: Array<{ attribute: string; direction: "asc" | "desc" }>;
      outputVariableName?: string;
    })
  | (MicroflowNodeBase & {
      type: "changeObject";
      objectVariable: string;
      memberChanges: Array<{ memberName: string; valueExpression: ExpressionSchema }>;
      commit: {
        enabled: boolean;
        withEvents: boolean;
        refreshInClient: boolean;
      };
    })
  | (MicroflowNodeBase & {
      type: "commitObject";
      targetVariableName: string;
    })
  | (MicroflowNodeBase & {
      type: "createVariable" | "changeVariable";
      variableName: string;
      valueExpression: ExpressionSchema;
    })
  | (MicroflowNodeBase & {
      type: "showMessage" | "validationFeedback";
      message: string;
      targetMember?: string;
    })
  | (MicroflowNodeBase & {
      type: "callWorkflow";
      workflowRef: Ref<"workflow">;
      arguments: ExpressionSchema[];
    })
  | (MicroflowNodeBase & {
      type: "callMicroflow";
      microflowRef: Ref<"microflow">;
      arguments: ExpressionSchema[];
    });

export interface MicroflowEdgeSchema {
  edgeId: string;
  fromNodeId: string;
  toNodeId: string;
  outcome?: "true" | "false" | string;
}

export interface MicroflowSchema {
  microflowId: string;
  moduleId: string;
  name: string;
  parameters: MicroflowParameterSchema[];
  returnType: DataTypeSchema;
  allowedRoles: Ref<"moduleRole">[];
  applyEntityAccess: boolean;
  concurrentExecution: {
    allowConcurrentExecution: boolean;
    onBlocked?: "showMessage" | "callMicroflow" | "throwError";
    blockedMessage?: string;
    blockedMicroflowRef?: Ref<"microflow">;
  };
  nodes: MicroflowNodeSchema[];
  edges: MicroflowEdgeSchema[];
}

export interface WorkflowParameterSchema {
  name: string;
  type: DataTypeSchema;
}

export type WorkflowNodeType =
  | "startEvent"
  | "endEvent"
  | "decision"
  | "userTask"
  | "callMicroflow"
  | "timer"
  | "annotation";

type WorkflowNodeBase = {
  nodeId: string;
  type: WorkflowNodeType;
  caption: string;
  position: { x: number; y: number };
};

export type WorkflowNodeSchema =
  | (WorkflowNodeBase & { type: "startEvent" | "endEvent" | "annotation" })
  | (WorkflowNodeBase & {
      type: "decision";
      expression: ExpressionSchema;
      outcomes?: string[];
    })
  | (WorkflowNodeBase & {
      type: "callMicroflow";
      microflowRef: Ref<"microflow">;
    })
  | (WorkflowNodeBase & {
      type: "timer";
      durationExpression?: ExpressionSchema;
    })
  | (WorkflowNodeBase & {
      type: "userTask";
      taskName: string;
      taskDescription?: string;
      taskPageRef?: Ref<"page">;
      targetUsers: Array<Ref<"userRole">>;
      dueDate?: ExpressionSchema;
      outcomes: Array<{ key: string; caption: string }>;
      onCreated?: Ref<"microflow">;
      onAssigned?: Ref<"microflow">;
      onCompleted?: Ref<"microflow">;
    });

export interface WorkflowEdgeSchema {
  edgeId: string;
  fromNodeId: string;
  toNodeId: string;
  sequence?: number;
  decisionOutcome?: string;
  taskOutcome?: string;
  timerOutcome?: string;
}

export interface WorkflowSchema {
  workflowId: string;
  moduleId: string;
  name: string;
  contextEntityRef: Ref<"entity">;
  parameters: WorkflowParameterSchema[];
  nodes: WorkflowNodeSchema[];
  edges: WorkflowEdgeSchema[];
}

export interface UserRoleSchema {
  roleId: string;
  name: string;
  moduleRoleRefs: Ref<"moduleRole">[];
}

export interface ModuleRoleSchema {
  roleId: string;
  moduleId: string;
  name: string;
}

export interface PageAccessRuleSchema {
  pageRef: Ref<"page">;
  roleRefs: Ref<"moduleRole">[];
}

export interface MicroflowAccessRuleSchema {
  microflowRef: Ref<"microflow">;
  roleRefs: Ref<"moduleRole">[];
}

export interface NanoflowAccessRuleSchema {
  nanoflowName: string;
  roleRefs: Ref<"moduleRole">[];
}

export interface SecuritySchema {
  securityLevel: "off" | "prototype" | "production";
  userRoles: UserRoleSchema[];
  moduleRoles: ModuleRoleSchema[];
  pageAccessRules: PageAccessRuleSchema[];
  microflowAccessRules: MicroflowAccessRuleSchema[];
  nanoflowAccessRules: NanoflowAccessRuleSchema[];
  entityAccessRules: EntityAccessRuleSchema[];
}

export interface ModuleSchema {
  moduleId: string;
  name: string;
  domainModel: DomainModelSchema;
  pages: PageSchema[];
  microflows: MicroflowSchema[];
  workflows: WorkflowSchema[];
  enumerations: EnumerationSchema[];
}

export interface RuntimeWidgetSchema {
  widgetId: string;
  widgetType: WidgetType;
  props: Record<string, unknown>;
  value?: unknown;
  children?: RuntimeWidgetSchema[];
}

export interface RuntimePageModel {
  pageId: string;
  pageName: string;
  rootWidget: RuntimeWidgetSchema;
  contextEntityRef?: Ref<"entity">;
}

export type RuntimeUiCommand =
  | { type: "showMessage"; level: "info" | "warning" | "error"; message: string }
  | { type: "showPage"; pageRef: Ref<"page"> }
  | { type: "closePage" }
  | { type: "refreshObject"; objectPath: string }
  | { type: "refreshList"; listPath: string }
  | { type: "validationFeedback"; targetPath: string; message: string }
  | { type: "openTaskPage"; workflowTaskId: string };

export interface ExecuteActionRequest {
  actionType: "callMicroflow" | "callWorkflow" | "showMessage";
  microflowRef?: Ref<"microflow">;
  workflowRef?: Ref<"workflow">;
  message?: string;
  arguments?: Array<{ name: string; value: unknown }>;
}

export interface ExecuteActionResponse {
  success: boolean;
  returnValue?: unknown;
  uiCommands: RuntimeUiCommand[];
  traceId?: string;
}

export interface FlowExecutionTraceStepSchema {
  stepId: string;
  nodeId: string;
  nodeType: string;
  expressionResults: Array<{ expression: string; result: unknown }>;
  permissionChecks: Array<{ check: string; allowed: boolean }>;
  databaseQueries: string[];
  uiCommands: RuntimeUiCommand[];
  inputSnapshot?: Record<string, unknown>;
  outputSnapshot?: Record<string, unknown>;
}

export interface FlowExecutionTraceSchema {
  traceId: string;
  flowType: "microflow" | "workflow";
  flowId: string;
  startedAt: string;
  endedAt?: string;
  status: "running" | "succeeded" | "failed";
  inputArguments: Record<string, unknown>;
  steps: FlowExecutionTraceStepSchema[];
}

export interface ValidationErrorSchema {
  severity: "error" | "warning" | "info";
  code: string;
  message: string;
  target: {
    kind: string;
    id: string;
    path?: string;
  };
  quickFixes?: Array<{ title: string; patch: string }>;
}

export interface SchemaMigrationPlan {
  fromVersion: string;
  toVersion: string;
  steps: Array<{ stepId: string; description: string; risk: "low" | "medium" | "high" }>;
}

export interface ExtensionManifestSchema {
  extensionId: string;
  name: string;
  version: string;
  contributes: {
    widgetTypes?: string[];
    microflowNodeTypes?: string[];
    workflowNodeTypes?: string[];
  };
}

export interface LowCodeAppSchema {
  appId: string;
  name: string;
  version: string;
  modules: ModuleSchema[];
  navigation: NavigationItemSchema[];
  security: SecuritySchema;
  extensions: ExtensionManifestSchema[];
}
