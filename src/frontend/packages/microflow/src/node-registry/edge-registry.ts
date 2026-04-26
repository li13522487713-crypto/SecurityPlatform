import type {
  MicroflowEdge,
  MicroflowEdgeKind,
  MicroflowEdgeStyle,
  MicroflowNode,
  MicroflowNodeKind,
  MicroflowPortKind,
  MicroflowRuntimeEdgeDto,
  MicroflowValidationIssue
} from "../schema/types";

export interface MicroflowEdgeRegistryItem {
  kind: MicroflowEdgeKind;
  title: string;
  titleZh: string;
  description: string;
  lineStyle: MicroflowEdgeStyle;
  colorToken: string;
  hasArrow: boolean;
  labelMode: "none" | "auto" | "editable" | "condition";
  sourcePortKinds: MicroflowPortKind[];
  targetPortKinds: MicroflowPortKind[];
  sourceNodeKinds: MicroflowNodeKind[];
  targetNodeKinds: MicroflowNodeKind[];
  runtimeEffect: "controlFlow" | "errorFlow" | "annotationOnly";
  validate: (edge: MicroflowEdge, context: { nodes: MicroflowNode[]; edges: MicroflowEdge[] }) => MicroflowValidationIssue[];
  toRuntimeDto: (edge: MicroflowEdge) => MicroflowRuntimeEdgeDto;
  fromRuntimeDto: (dto: MicroflowRuntimeEdgeDto) => MicroflowEdge;
}

function issue(code: string, message: string, edgeId: string): MicroflowValidationIssue {
  return { id: `${code}:${edgeId}`, code, message, severity: "error", edgeId };
}

function nodeKind(node: MicroflowNode | undefined): MicroflowNodeKind | undefined {
  if (!node) {
    return undefined;
  }
  if (node.kind) {
    return node.kind;
  }
  if (["startEvent", "endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(node.type)) {
    return "event";
  }
  return node.type as MicroflowNodeKind;
}

function validateBase(edge: MicroflowEdge, context: { nodes: MicroflowNode[] }, item: MicroflowEdgeRegistryItem): MicroflowValidationIssue[] {
  const source = context.nodes.find(node => node.id === edge.sourceNodeId);
  const target = context.nodes.find(node => node.id === edge.targetNodeId);
  const sourceKind = nodeKind(source);
  const targetKind = nodeKind(target);
  const issues: MicroflowValidationIssue[] = [];
  if (!source) {
    issues.push(issue("MF_EDGE_SOURCE_MISSING", "Flow source node does not exist.", edge.id));
  }
  if (!target) {
    issues.push(issue("MF_EDGE_TARGET_MISSING", "Flow target node does not exist.", edge.id));
  }
  if (sourceKind && !item.sourceNodeKinds.includes(sourceKind)) {
    issues.push(issue("MF_EDGE_SOURCE_KIND", `${item.title} cannot start from ${source?.type}.`, edge.id));
  }
  if (targetKind && !item.targetNodeKinds.includes(targetKind)) {
    issues.push(issue("MF_EDGE_TARGET_KIND", `${item.title} cannot target ${target?.type}.`, edge.id));
  }
  return issues;
}

function toRuntimeDto(edge: MicroflowEdge, runtimeEffect: MicroflowEdgeRegistryItem["runtimeEffect"]): MicroflowRuntimeEdgeDto {
  return {
    edgeId: edge.id,
    type: edge.type,
    sourceNodeId: edge.sourceNodeId,
    targetNodeId: edge.targetNodeId,
    label: edge.label,
    sourcePortId: edge.sourcePortId,
    targetPortId: edge.targetPortId,
    conditionValue: edge.conditionValue,
    errorHandlingType: edge.errorHandlingType,
    runtimeEffect
  };
}

function fromRuntimeDto(dto: MicroflowRuntimeEdgeDto): MicroflowEdge {
  return {
    id: dto.edgeId,
    type: dto.type,
    sourceNodeId: dto.sourceNodeId,
    sourcePortId: dto.sourcePortId,
    targetNodeId: dto.targetNodeId,
    targetPortId: dto.targetPortId,
    label: dto.label,
    conditionValue: dto.conditionValue,
    errorHandlingType: dto.errorHandlingType
  } as MicroflowEdge;
}

const executableTargets: MicroflowNodeKind[] = ["activity", "decision", "objectTypeDecision", "loop", "merge", "event"];
const executableSources: MicroflowNodeKind[] = ["activity", "decision", "objectTypeDecision", "loop", "merge", "event"];

function make(item: Omit<MicroflowEdgeRegistryItem, "validate" | "toRuntimeDto" | "fromRuntimeDto"> & {
  validate?: MicroflowEdgeRegistryItem["validate"];
}): MicroflowEdgeRegistryItem {
  return {
    ...item,
    validate: item.validate ?? ((edge, context) => validateBase(edge, context, registryByKind.get(item.kind) ?? { ...item, validate: () => [], toRuntimeDto: () => toRuntimeDto(edge, item.runtimeEffect), fromRuntimeDto })),
    toRuntimeDto: edge => toRuntimeDto(edge, item.runtimeEffect),
    fromRuntimeDto
  };
}

export const defaultMicroflowEdgeRegistry: MicroflowEdgeRegistryItem[] = [
  make({
    kind: "sequence",
    title: "Sequence Flow",
    titleZh: "顺序流",
    description: "Normal one-way control flow between executable elements.",
    lineStyle: "solid",
    colorToken: "#4e5969",
    hasArrow: true,
    labelMode: "editable",
    sourcePortKinds: ["sequenceOut", "loopOut", "loopBodyIn"],
    targetPortKinds: ["sequenceIn", "loopIn", "loopBodyOut"],
    sourceNodeKinds: executableSources,
    targetNodeKinds: executableTargets,
    runtimeEffect: "controlFlow"
  }),
  make({
    kind: "decisionCondition",
    title: "Decision Condition Flow",
    titleZh: "决策条件流",
    description: "Decision branch selected by boolean, enumeration, empty, or custom condition.",
    lineStyle: "solid",
    colorToken: "#165dff",
    hasArrow: true,
    labelMode: "condition",
    sourcePortKinds: ["decisionOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    sourceNodeKinds: ["decision"],
    targetNodeKinds: executableTargets,
    runtimeEffect: "controlFlow",
    validate: (edge, context) => [
      ...validateBase(edge, context, defaultMicroflowEdgeRegistry[1]),
      ...(!edge.conditionValue ? [issue("MF_DECISION_EDGE_CONDITION", "Decision Condition Flow must configure conditionValue.", edge.id)] : [])
    ]
  }),
  make({
    kind: "objectTypeCondition",
    title: "Object Type Condition Flow",
    titleZh: "对象类型条件流",
    description: "Object type decision branch selected by specialization entity, empty, or fallback.",
    lineStyle: "solid",
    colorToken: "#722ed1",
    hasArrow: true,
    labelMode: "condition",
    sourcePortKinds: ["objectTypeOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    sourceNodeKinds: ["objectTypeDecision"],
    targetNodeKinds: executableTargets,
    runtimeEffect: "controlFlow",
    validate: (edge, context) => [
      ...validateBase(edge, context, defaultMicroflowEdgeRegistry[2]),
      ...(!edge.conditionValue || edge.conditionValue.kind !== "objectType" ? [issue("MF_OBJECT_TYPE_EDGE_CONDITION", "Object Type Condition Flow must configure objectType conditionValue.", edge.id)] : [])
    ]
  }),
  make({
    kind: "errorHandler",
    title: "Error Handler Flow",
    titleZh: "错误处理流",
    description: "Custom error branch from an element that supports error handling.",
    lineStyle: "dashed",
    colorToken: "#f93920",
    hasArrow: true,
    labelMode: "auto",
    sourcePortKinds: ["errorOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    sourceNodeKinds: ["activity", "decision", "objectTypeDecision", "loop"],
    targetNodeKinds: executableTargets,
    runtimeEffect: "errorFlow",
    validate: (edge, context) => {
      const source = context.nodes.find(node => node.id === edge.sourceNodeId);
      const base = validateBase(edge, context, defaultMicroflowEdgeRegistry[3]);
      if (!source) {
        return base;
      }
      const supportsError = source.type === "activity" ? source.config.supportsErrorFlow === true : ["decision", "objectTypeDecision", "loop"].includes(source.type);
      return supportsError ? base : [...base, issue("MF_ERROR_FLOW_SOURCE", "Error Handler Flow source must support error handling.", edge.id)];
    }
  }),
  make({
    kind: "annotation",
    title: "Annotation Flow",
    titleZh: "注释流",
    description: "Documentation-only attachment flow that does not affect execution.",
    lineStyle: "dashed",
    colorToken: "#86909c",
    hasArrow: false,
    labelMode: "editable",
    sourcePortKinds: ["annotation", "sequenceOut", "decisionOut", "objectTypeOut", "errorOut"],
    targetPortKinds: ["annotation", "sequenceIn", "loopIn"],
    sourceNodeKinds: ["annotation", "activity", "decision", "objectTypeDecision", "loop", "merge", "event", "parameter"],
    targetNodeKinds: ["annotation", "activity", "decision", "objectTypeDecision", "loop", "merge", "event", "parameter"],
    runtimeEffect: "annotationOnly",
    validate: (edge, context) => {
      const base = validateBase(edge, context, defaultMicroflowEdgeRegistry[4]);
      const source = context.nodes.find(node => node.id === edge.sourceNodeId);
      const target = context.nodes.find(node => node.id === edge.targetNodeId);
      return source?.type === "annotation" || target?.type === "annotation"
        ? base
        : [...base, issue("MF_ANNOTATION_EDGE_ENDPOINT", "Annotation Flow requires source or target to be an Annotation.", edge.id)];
    }
  })
];

export const registryByKind = new Map(defaultMicroflowEdgeRegistry.map(item => [item.kind, item]));

export function getMicroflowEdgeRegistryItem(kind: MicroflowEdgeKind): MicroflowEdgeRegistryItem | undefined {
  return registryByKind.get(kind);
}

export function createMicroflowEdge(input: Omit<MicroflowEdge, "id"> & { id?: string }): MicroflowEdge {
  return {
    id: input.id ?? `edge-${Date.now()}-${Math.round(Math.random() * 10000)}`,
    kind: input.type,
    ...input
  } as MicroflowEdge;
}
