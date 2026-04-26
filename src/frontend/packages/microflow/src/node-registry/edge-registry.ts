import type { MicroflowEditorEdge, MicroflowFlow, MicroflowValidationIssue } from "../schema/types";

export type MicroflowFlowRegistryKind = "sequence" | "annotation";
export type MicroflowDerivedEdgeKind = "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler" | "annotation";

export interface MicroflowEdgeRegistryItem {
  kind: MicroflowFlowRegistryKind;
  officialType: "Microflows$SequenceFlow" | "Microflows$AnnotationFlow";
  title: string;
  titleZh: string;
  description: string;
  derivedKinds: MicroflowDerivedEdgeKind[];
  validate: (flow: MicroflowFlow, context: { flows: MicroflowFlow[] }) => MicroflowValidationIssue[];
}

function issue(code: string, message: string, flowId: string): MicroflowValidationIssue {
  return { id: `${code}:${flowId}`, code, severity: "error", message, flowId };
}

function validateSequence(flow: MicroflowFlow): MicroflowValidationIssue[] {
  if (flow.kind !== "sequence") {
    return [issue("MF_FLOW_KIND_INVALID", "Expected sequence flow.", flow.id)];
  }
  const issues: MicroflowValidationIssue[] = [];
  if (!flow.originObjectId || !flow.destinationObjectId) {
    issues.push(issue("MF_FLOW_ENDPOINT_REQUIRED", "SequenceFlow requires origin and destination object ids.", flow.id));
  }
  if (flow.isErrorHandler && flow.editor.edgeKind !== "errorHandler") {
    issues.push(issue("MF_FLOW_ERROR_KIND_MISMATCH", "Error handler SequenceFlow must use editor.edgeKind=errorHandler.", flow.id));
  }
  if (flow.editor.edgeKind === "errorHandler" && !flow.isErrorHandler) {
    issues.push(issue("MF_FLOW_ERROR_FLAG_REQUIRED", "edgeKind=errorHandler requires isErrorHandler=true.", flow.id));
  }
  return issues;
}

function validateAnnotation(flow: MicroflowFlow): MicroflowValidationIssue[] {
  if (flow.kind !== "annotation") {
    return [issue("MF_FLOW_KIND_INVALID", "Expected annotation flow.", flow.id)];
  }
  const issues: MicroflowValidationIssue[] = [];
  if (!flow.originObjectId || !flow.destinationObjectId) {
    issues.push(issue("MF_FLOW_ENDPOINT_REQUIRED", "AnnotationFlow requires origin and destination object ids.", flow.id));
  }
  return issues;
}

export const defaultMicroflowEdgeRegistry: MicroflowEdgeRegistryItem[] = [
  {
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    title: "Sequence Flow",
    titleZh: "顺序流",
    description: "Official execution flow. Derived edge kinds are editor-only metadata.",
    derivedKinds: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"],
    validate: (flow: MicroflowFlow) => validateSequence(flow)
  },
  {
    kind: "annotation",
    officialType: "Microflows$AnnotationFlow",
    title: "Annotation Flow",
    titleZh: "注释流",
    description: "Official documentation flow without runtime behavior.",
    derivedKinds: ["annotation"],
    validate: (flow: MicroflowFlow) => validateAnnotation(flow)
  }
];

const registryByKind = new Map(defaultMicroflowEdgeRegistry.map(item => [item.kind, item]));

export function getMicroflowEdgeRegistryItem(kind: MicroflowFlowRegistryKind): MicroflowEdgeRegistryItem | undefined {
  return registryByKind.get(kind);
}

export function deriveEdgeKind(flow: MicroflowFlow): MicroflowDerivedEdgeKind {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  if (flow.isErrorHandler) {
    return "errorHandler";
  }
  return flow.editor.edgeKind;
}

export function edgeStyleByKind(kind: MicroflowDerivedEdgeKind): MicroflowEditorEdge["style"] {
  if (kind === "annotation") {
    return { strokeType: "dashed", colorToken: "#86909c", arrow: false };
  }
  if (kind === "errorHandler") {
    return { strokeType: "dashed", colorToken: "#f93920", arrow: true };
  }
  if (kind === "decisionCondition") {
    return { strokeType: "solid", colorToken: "#165dff", arrow: true };
  }
  if (kind === "objectTypeCondition") {
    return { strokeType: "solid", colorToken: "#722ed1", arrow: true };
  }
  return { strokeType: "solid", colorToken: "#4e5969", arrow: true };
}
