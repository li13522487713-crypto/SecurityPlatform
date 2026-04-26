import type {
  MicroflowActivityConfig,
  MicroflowActivityType,
  MicroflowEdgeType,
  MicroflowNode,
  MicroflowNodeCategory,
  MicroflowNodeKind,
  MicroflowPort,
  MicroflowPosition,
  MicroflowPropertyFormMetadata,
  MicroflowRenderMetadata,
  MicroflowRuntimeNodeDto,
  MicroflowValidationIssue
} from "../schema/types";

export interface MicroflowNodeRegistryEntry<TConfig extends object = Record<string, unknown>> {
  type: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  title: string;
  category: MicroflowNodeCategory;
  group: "Events" | "Decisions" | "Activities" | "Loop" | "Parameters" | "Annotations";
  subgroup?: string;
  iconKey: string;
  defaultConfig: TConfig;
  ports: MicroflowPort[];
  render: MicroflowRenderMetadata;
  propertyForm: MicroflowPropertyFormMetadata;
  validate: (node: MicroflowNode) => MicroflowValidationIssue[];
  toRuntimeDto: (node: MicroflowNode) => MicroflowRuntimeNodeDto;
  fromRuntimeDto: (dto: MicroflowRuntimeNodeDto, position: MicroflowPosition) => MicroflowNode;
}

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
      config: entry.defaultConfig as MicroflowActivityConfig
    };
  }

  return {
    ...base,
    type: entry.type,
    config: entry.defaultConfig
  } as MicroflowNode;
}

function createEntry<TConfig extends object>(entry: Omit<MicroflowNodeRegistryEntry<TConfig>, "validate" | "toRuntimeDto" | "fromRuntimeDto"> & {
  validate?: MicroflowNodeRegistryEntry<TConfig>["validate"];
}): MicroflowNodeRegistryEntry<TConfig> {
  return {
    ...entry,
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

function eventEntry(type: Extract<MicroflowNodeKind, "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent">, title: string, tone: MicroflowRenderMetadata["tone"], ports: MicroflowPort[]): MicroflowNodeRegistryEntry {
  return createEntry({
    type,
    title,
    category: "event",
    group: "Events",
    iconKey: type,
    defaultConfig: {},
    ports,
    render: { iconKey: type, shape: "event", tone, width: 116, height: 70 },
    propertyForm: { formKey: "event", sections: ["General", "Return"] }
  });
}

function activityEntry(activityType: MicroflowActivityType, title: string, subgroup: string, config: Partial<MicroflowActivityConfig> = {}, edgeTypes: MicroflowEdgeType[] = ["sequence"]): MicroflowNodeRegistryEntry<MicroflowActivityConfig> {
  const supportsErrorFlow = edgeTypes.includes("error");
  return createEntry<MicroflowActivityConfig>({
    type: "activity",
    activityType,
    title,
    category: "activity",
    group: "Activities",
    subgroup,
    iconKey: activityType,
    defaultConfig: {
      activityType,
      supportsErrorFlow,
      errorHandling: supportsErrorFlow ? { mode: "rollback", errorVariableName: "latestError" } : undefined,
      ...config
    },
    ports: supportsErrorFlow ? [inputPort, outputPort, errorPort] : [inputPort, outputPort],
    render: { iconKey: activityType, shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: `${subgroup.toLowerCase()}Activity`, sections: ["General", "Input", "Error Handling"] }
  });
}

export const microflowNodeRegistries: MicroflowNodeRegistryEntry[] = [
  eventEntry("startEvent", "Start", "success", [outputPort]),
  eventEntry("endEvent", "End", "danger", [inputPort]),
  eventEntry("errorEvent", "Error", "danger", [inputPort, outputPort]),
  eventEntry("breakEvent", "Break", "warning", [inputPort]),
  eventEntry("continueEvent", "Continue", "warning", [inputPort]),
  createEntry({
    type: "decision",
    title: "Decision",
    category: "decision",
    group: "Decisions",
    iconKey: "decision",
    defaultConfig: { expression: { id: "expr-decision", language: "mendix", text: "", referencedVariables: [] } },
    ports: [inputPort, { ...outputPort, id: "true", label: "True" }, { ...outputPort, id: "false", label: "False" }],
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 132, height: 96 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Outcomes"] }
  }),
  createEntry({
    type: "merge",
    title: "Merge",
    category: "merge",
    group: "Decisions",
    iconKey: "merge",
    defaultConfig: { strategy: "firstAvailable" },
    ports: [inputPort, outputPort],
    render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 },
    propertyForm: { formKey: "merge", sections: ["General"] }
  }),
  createEntry({
    type: "loop",
    title: "Loop",
    category: "loop",
    group: "Loop",
    iconKey: "loop",
    defaultConfig: { iterableVariableName: "", itemVariableName: "currentItem" },
    ports: [inputPort, outputPort],
    render: { iconKey: "loop", shape: "loop", tone: "info", width: 164, height: 78 },
    propertyForm: { formKey: "loop", sections: ["Collection", "Item"] }
  }),
  createEntry({
    type: "parameter",
    title: "Parameter",
    category: "parameter",
    group: "Parameters",
    iconKey: "parameter",
    defaultConfig: { parameter: { id: "parameter", name: "input", required: true, type: { kind: "unknown", name: "Unknown" } } },
    ports: [outputPort],
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 150, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  }),
  createEntry({
    type: "annotation",
    title: "Annotation",
    category: "annotation",
    group: "Annotations",
    iconKey: "annotation",
    defaultConfig: { text: "Describe the intent of this part of the microflow." },
    ports: [annotationPort],
    render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 220, height: 100 },
    propertyForm: { formKey: "annotation", sections: ["Text"] }
  }),
  activityEntry("objectCreate", "Object Create", "Object", { entity: "Sales.Order", objectVariableName: "newOrder" }, ["sequence", "error"]),
  activityEntry("objectChange", "Object Change", "Object", { objectVariableName: "order" }, ["sequence", "error"]),
  activityEntry("objectCommit", "Object Commit", "Object", { objectVariableName: "order" }, ["sequence", "error"]),
  activityEntry("objectDelete", "Object Delete", "Object", { objectVariableName: "order" }, ["sequence", "error"]),
  activityEntry("objectRetrieve", "Object Retrieve", "Object", { entity: "Sales.Order", listVariableName: "orders" }, ["sequence", "error"]),
  activityEntry("objectRollback", "Object Rollback", "Object", { objectVariableName: "order" }, ["sequence", "error"]),
  activityEntry("variableCreate", "Variable Create", "Variable", { variableName: "result", variableType: { kind: "primitive", name: "String" } }),
  activityEntry("variableChange", "Variable Change", "Variable", { variableName: "result" }),
  activityEntry("callMicroflow", "Call Microflow", "Call", { targetMicroflowId: "MF_ValidateOrder" }, ["sequence", "error"]),
  activityEntry("callRest", "Call REST", "Integration", { method: "POST", url: "/api/orders/sync" }, ["sequence", "error"]),
  activityEntry("logMessage", "Log Message", "Logging", { messageExpression: { id: "expr-log", language: "plainText", text: "Order processed", referencedVariables: [] } }),
  activityEntry("showPage", "Show Page", "Client", { pageName: "Order.Detail" }),
  activityEntry("closePage", "Close Page", "Client")
];

export const microflowNodeRegistryByKey = new Map(
  microflowNodeRegistries.map(entry => [entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type, entry])
);

export function createMicroflowNodeFromRegistry(
  registryKey: string,
  id: string,
  position: MicroflowPosition
): MicroflowNode {
  const entry = microflowNodeRegistryByKey.get(registryKey);
  if (!entry) {
    throw new Error(`Unknown microflow registry key: ${registryKey}`);
  }
  return createNodeFromRegistry(entry, id, position);
}
