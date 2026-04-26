import type {
  MicroflowActivityConfig,
  MicroflowActivityType,
  MicroflowEdgeType,
  MicroflowNode,
  MicroflowNodeCategory as MicroflowSchemaNodeCategory,
  MicroflowNodeKind,
  MicroflowPort,
  MicroflowPosition,
  MicroflowPropertyFormMetadata,
  MicroflowRenderMetadata,
  MicroflowRuntimeNodeDto,
  MicroflowValidationIssue
} from "../schema/types";

export type MicroflowNodePanelCategoryKey =
  | "events"
  | "decisions"
  | "activities"
  | "loop"
  | "parameters"
  | "annotations";

export type MicroflowNodeGroup =
  | "object"
  | "list"
  | "call"
  | "variable"
  | "client"
  | "integration"
  | "logging";

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
  nodeType: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  registryKey: string;
  title: string;
  defaultConfig: Record<string, unknown>;
  sourcePanel: "microflow-node-panel";
}

export type MicroflowNodeFilterKey = "all" | "favorites" | "enabled" | MicroflowNodePanelCategoryKey;

export interface MicroflowNodeCategoryDefinition {
  key: MicroflowNodePanelCategoryKey;
  label: string;
  category: MicroflowSchemaNodeCategory | "decision";
  groups?: Array<{ key: MicroflowNodeGroup; label: string }>;
}

export interface MicroflowNodeRegistryEntry<TConfig extends object = Record<string, unknown>> {
  type: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  title: string;
  description: string;
  category: MicroflowSchemaNodeCategory;
  group: "Events" | "Decisions" | "Activities" | "Loop" | "Parameters" | "Annotations";
  subgroup?: string;
  iconKey: string;
  keywords: string[];
  defaultConfig: TConfig;
  ports: MicroflowPort[];
  enabled: boolean;
  disabledReason?: string;
  documentation?: string;
  supportsErrorHandling?: boolean;
  inputs?: string[];
  outputs?: string[];
  useCases?: string[];
  render: MicroflowRenderMetadata;
  propertyForm: MicroflowPropertyFormMetadata;
  validate: (node: MicroflowNode) => MicroflowValidationIssue[];
  toRuntimeDto: (node: MicroflowNode) => MicroflowRuntimeNodeDto;
  fromRuntimeDto: (dto: MicroflowRuntimeNodeDto, position: MicroflowPosition) => MicroflowNode;
}

export type MicroflowNodeRegistryItem<TConfig extends object = Record<string, unknown>> = MicroflowNodeRegistryEntry<TConfig>;

export const microflowNodeCategoryDefinitions: MicroflowNodeCategoryDefinition[] = [
  { key: "events", label: "Events", category: "event" },
  { key: "decisions", label: "Decisions", category: "decision" },
  {
    key: "activities",
    label: "Activities",
    category: "activity",
    groups: [
      { key: "object", label: "Object" },
      { key: "list", label: "List" },
      { key: "call", label: "Call" },
      { key: "variable", label: "Variable" },
      { key: "client", label: "Client" },
      { key: "integration", label: "Integration" },
      { key: "logging", label: "Logging" }
    ]
  },
  { key: "loop", label: "Loop", category: "loop" },
  { key: "parameters", label: "Parameters", category: "parameter" },
  { key: "annotations", label: "Annotations", category: "annotation" }
];

const inputPort: MicroflowPort = {
  id: "in",
  label: "In",
  direction: "input",
  edgeTypes: ["sequence", "error"]
};

const outputPort: MicroflowPort = {
  id: "out",
  label: "Out",
  direction: "output",
  edgeTypes: ["sequence"]
};

const errorPort: MicroflowPort = {
  id: "error",
  label: "Error",
  direction: "output",
  edgeTypes: ["error"]
};

const annotationPort: MicroflowPort = {
  id: "note",
  label: "Note",
  direction: "output",
  edgeTypes: ["annotation"]
};

function dtoConfig(config: object): Record<string, unknown> {
  return { ...config };
}

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

function createNodeFromRegistry(
  entry: MicroflowNodeRegistryEntry,
  id: string,
  position: MicroflowPosition,
  title = entry.title
): MicroflowNode {
  const base = {
    id,
    title,
    description: entry.description,
    category: entry.category,
    position,
    ports: entry.ports,
    render: entry.render,
    propertyForm: entry.propertyForm
  };

  if (entry.type === "activity") {
    return {
      ...base,
      type: "activity",
      config: cloneConfig(entry.defaultConfig as MicroflowActivityConfig)
    };
  }

  return {
    ...base,
    type: entry.type,
    config: cloneConfig(entry.defaultConfig)
  } as MicroflowNode;
}

type MicroflowNodeRegistryEntryInput<TConfig extends object> = Omit<
  MicroflowNodeRegistryEntry<TConfig>,
  | "validate"
  | "toRuntimeDto"
  | "fromRuntimeDto"
  | "keywords"
  | "enabled"
  | "supportsErrorHandling"
  | "inputs"
  | "outputs"
  | "useCases"
> & Partial<Pick<
  MicroflowNodeRegistryEntry<TConfig>,
  "keywords" | "enabled" | "supportsErrorHandling" | "inputs" | "outputs" | "useCases"
>>;

function createEntry<TConfig extends object>(entry: MicroflowNodeRegistryEntryInput<TConfig> & {
  validate?: MicroflowNodeRegistryEntry<TConfig>["validate"];
}): MicroflowNodeRegistryEntry<TConfig> {
  return {
    ...entry,
    keywords: entry.keywords ?? [entry.title, entry.description, entry.group, entry.subgroup, entry.iconKey].filter((value): value is string => Boolean(value)),
    enabled: entry.enabled ?? true,
    supportsErrorHandling: entry.supportsErrorHandling ?? false,
    inputs: entry.inputs ?? entry.ports.filter(port => port.direction === "input").map(port => port.label),
    outputs: entry.outputs ?? entry.ports.filter(port => port.direction === "output").map(port => port.label),
    useCases: entry.useCases ?? [],
    validate: entry.validate ?? baseValidate,
    toRuntimeDto: node => ({
      nodeId: node.id,
      type: node.type,
      activityType: node.type === "activity" ? node.config.activityType : undefined,
      title: node.title,
      config: dtoConfig(node.config)
    }),
    fromRuntimeDto: (dto, position) => createNodeFromRegistry(entry as MicroflowNodeRegistryEntry, dto.nodeId, position, dto.title)
  };
}

function eventEntry(type: Extract<MicroflowNodeKind, "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent">, title: string, tone: MicroflowRenderMetadata["tone"], ports: MicroflowPort[], description: string): MicroflowNodeRegistryEntry {
  return createEntry({
    type,
    title,
    description,
    category: "event",
    group: "Events",
    iconKey: type,
    defaultConfig: {},
    ports,
    keywords: [title, "event", type],
    documentation: `${title} marks a control point in the microflow execution path.`,
    useCases: ["Control microflow start, end, exception, and loop interruption behavior."],
    render: { iconKey: type, shape: "event", tone, width: 116, height: 70 },
    propertyForm: { formKey: "event", sections: ["General", "Return"] }
  });
}

function activityEntry(activityType: MicroflowActivityType, title: string, subgroup: string, config: Partial<MicroflowActivityConfig> = {}, edgeTypes: MicroflowEdgeType[] = ["sequence"], description = `${title} activity.`): MicroflowNodeRegistryEntry<MicroflowActivityConfig> {
  const supportsErrorFlow = edgeTypes.includes("error");
  return createEntry<MicroflowActivityConfig>({
    type: "activity",
    activityType,
    title,
    description,
    category: "activity",
    group: "Activities",
    subgroup,
    iconKey: activityType,
    keywords: [title, description, subgroup, activityType, "activity"],
    defaultConfig: {
      activityType,
      supportsErrorFlow,
      errorHandling: supportsErrorFlow ? { mode: "rollback", errorVariableName: "latestError" } : undefined,
      ...config
    },
    ports: supportsErrorFlow ? [inputPort, outputPort, errorPort] : [inputPort, outputPort],
    enabled: config.reserved ? false : true,
    disabledReason: config.reserved ? "This node is reserved for a later microflow runtime capability." : undefined,
    documentation: `${title} is an ${subgroup} activity. ${description}`,
    supportsErrorHandling: supportsErrorFlow,
    useCases: [`Use when the microflow needs ${description.toLowerCase()}`],
    render: { iconKey: activityType, shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: `${subgroup.toLowerCase()}Activity`, sections: ["General", "Input", "Error Handling"] }
  });
}

export const microflowNodeRegistries: MicroflowNodeRegistryEntry[] = [
  eventEntry("startEvent", "Start Event", "success", [outputPort], "Starts the microflow execution."),
  eventEntry("endEvent", "End Event", "danger", [inputPort], "Ends the microflow and optionally returns a value."),
  eventEntry("errorEvent", "Error Event", "danger", [inputPort, outputPort], "Handles an error path inside the microflow."),
  eventEntry("breakEvent", "Break", "warning", [inputPort], "Breaks out of the nearest loop."),
  eventEntry("continueEvent", "Continue Event", "warning", [inputPort], "Continues with the next loop iteration."),
  createEntry({
    type: "decision",
    title: "Decision",
    description: "Branches the microflow with a boolean or expression outcome.",
    category: "decision",
    group: "Decisions",
    iconKey: "decision",
    keywords: ["Decision", "branch", "condition", "expression", "if"],
    defaultConfig: { expression: { id: "expr-decision", language: "mendix", text: "", referencedVariables: [] } },
    ports: [inputPort, { ...outputPort, id: "true", label: "True" }, { ...outputPort, id: "false", label: "False" }],
    documentation: "Evaluates an expression and routes execution to one of the decision outcomes.",
    useCases: ["Model business rules, validations, and conditional paths."],
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 132, height: 96 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Outcomes"] }
  }),
  createEntry({
    type: "merge",
    title: "Merge",
    description: "Combines multiple branches into a single continuation.",
    category: "merge",
    group: "Decisions",
    iconKey: "merge",
    keywords: ["Merge", "join", "branches", "combine"],
    defaultConfig: { strategy: "firstAvailable" },
    ports: [inputPort, outputPort],
    documentation: "Combines multiple decision branches into one continuation point.",
    useCases: ["Reconnect branches after a decision."],
    render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 },
    propertyForm: { formKey: "merge", sections: ["General"] }
  }),
  createEntry({
    type: "loop",
    title: "Loop",
    description: "Iterates over a list variable and exposes the current item.",
    category: "loop",
    group: "Loop",
    iconKey: "loop",
    keywords: ["Loop", "iterate", "for each", "list"],
    defaultConfig: { iterableVariableName: "", itemVariableName: "currentItem" },
    ports: [inputPort, outputPort],
    documentation: "Iterates over a list and exposes the current item to child activities.",
    useCases: ["Process every object in a retrieved list."],
    render: { iconKey: "loop", shape: "loop", tone: "info", width: 164, height: 78 },
    propertyForm: { formKey: "loop", sections: ["Collection", "Item"] }
  }),
  createEntry({
    type: "parameter",
    title: "Parameter",
    description: "Defines an input parameter for the microflow.",
    category: "parameter",
    group: "Parameters",
    iconKey: "parameter",
    keywords: ["Parameter", "input", "argument"],
    defaultConfig: { parameter: { id: "parameter", name: "input", required: true, type: { kind: "unknown", name: "Unknown" } } },
    ports: [outputPort],
    documentation: "Defines an input parameter that callers can provide when invoking the microflow.",
    useCases: ["Expose objects, primitive values, or lists to the microflow."],
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 150, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  }),
  createEntry({
    type: "annotation",
    title: "Annotation",
    description: "Adds documentation to the canvas without affecting execution.",
    category: "annotation",
    group: "Annotations",
    iconKey: "annotation",
    keywords: ["Annotation", "note", "comment", "documentation"],
    defaultConfig: { text: "Describe the intent of this part of the microflow." },
    ports: [annotationPort],
    documentation: "Documents the canvas without changing runtime behavior.",
    useCases: ["Explain business intent or implementation notes for teammates."],
    render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 220, height: 100 },
    propertyForm: { formKey: "annotation", sections: ["Text"] }
  }),
  activityEntry("objectCreate", "Create Object", "Object", { entity: "Sales.Order", objectVariableName: "newOrder" }, ["sequence", "error"], "Creates an object instance and stores it in a variable."),
  activityEntry("objectChange", "Change Object", "Object", { objectVariableName: "order" }, ["sequence", "error"], "Changes values on an existing object."),
  activityEntry("objectCommit", "Commit Object", "Object", { objectVariableName: "order" }, ["sequence", "error"], "Persists an object and optionally triggers events."),
  activityEntry("objectDelete", "Delete Object", "Object", { objectVariableName: "order" }, ["sequence", "error"], "Deletes an object from persistence."),
  activityEntry("objectRetrieve", "Retrieve Object", "Object", { entity: "Sales.Order", listVariableName: "orders" }, ["sequence", "error"], "Retrieves one or more objects from persistence."),
  activityEntry("objectRollback", "Rollback Object", "Object", { objectVariableName: "order" }, ["sequence", "error"], "Rolls back changes made to an object."),
  activityEntry("listOperation", "List Operation", "List", { listVariableName: "orders", reserved: true }, ["sequence"], "Reserved entry for list filter/map/sort operations."),
  activityEntry("listAggregate", "List Aggregate", "List", { listVariableName: "orders", reserved: true }, ["sequence"], "Reserved entry for count/sum/min/max list aggregation."),
  activityEntry("variableCreate", "Create Variable", "Variable", { variableName: "result", variableType: { kind: "primitive", name: "String" } }, ["sequence"], "Creates a microflow variable."),
  activityEntry("variableChange", "Change Variable", "Variable", { variableName: "result" }, ["sequence"], "Changes the value of a variable."),
  activityEntry("callMicroflow", "Call Microflow", "Call", { targetMicroflowId: "MF_ValidateOrder" }, ["sequence", "error"]),
  activityEntry("callNanoflow", "Call Nanoflow", "Call", { targetMicroflowId: "NF_ClientAction", reserved: true }, ["sequence"], "Reserved entry for future client-side nanoflow calls."),
  activityEntry("callRest", "Call REST", "Integration", { method: "POST", url: "/api/orders/sync" }, ["sequence", "error"]),
  activityEntry("logMessage", "Log Message", "Logging", { messageExpression: { id: "expr-log", language: "plainText", text: "Order processed", referencedVariables: [] } }),
  activityEntry("showPage", "Show Page", "Client", { pageName: "Order.Detail" }),
  activityEntry("closePage", "Close Page", "Client")
];

export const microflowNodeRegistryByKey = new Map(
  microflowNodeRegistries.map(entry => [entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type, entry])
);

export const defaultMicroflowNodeRegistry = microflowNodeRegistries;

export function getMicroflowNodeRegistryKey(entry: Pick<MicroflowNodeRegistryEntry, "type" | "activityType">): string {
  return entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type;
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

export function getMicroflowNodeCategories(registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodeRegistry): MicroflowNodeCategoryDefinition[] {
  const available = new Set(registry.map(categoryKeyForEntry));
  return microflowNodeCategoryDefinitions.filter(category => available.has(category.key));
}

export function searchMicroflowNodes(keyword: string, registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodeRegistry): MicroflowNodeRegistryEntry[] {
  const normalized = keyword.trim().toLowerCase();
  if (!normalized) {
    return registry;
  }
  return registry.filter(entry => [
    entry.title,
    entry.description,
    entry.group,
    entry.subgroup,
    entry.iconKey,
    entry.documentation,
    ...entry.keywords
  ].filter((value): value is string => Boolean(value)).join(" ").toLowerCase().includes(normalized));
}

export function groupMicroflowNodesByCategory(registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodeRegistry): Array<{
  category: MicroflowNodeCategoryDefinition;
  entries: MicroflowNodeRegistryEntry[];
  groups: Array<{ key: MicroflowNodeGroup; label: string; entries: MicroflowNodeRegistryEntry[] }>;
}> {
  return getMicroflowNodeCategories(registry).map(category => {
    const entries = registry.filter(entry => categoryKeyForEntry(entry) === category.key);
    const groups = (category.groups ?? []).map(group => ({
      ...group,
      entries: entries.filter(entry => entry.subgroup?.toLowerCase() === group.key)
    })).filter(group => group.entries.length > 0);
    return { category, entries, groups };
  }).filter(category => category.entries.length > 0);
}

export function getMicroflowNodeByType(
  type: MicroflowNodeKind,
  activityType?: MicroflowActivityType,
  registry: MicroflowNodeRegistryEntry[] = defaultMicroflowNodeRegistry
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
