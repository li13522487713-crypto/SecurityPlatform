import type {
  MicroflowActionKind,
  MicroflowCaseValue,
  MicroflowFlow,
  MicroflowObjectKind,
  MicroflowPoint,
  MicroflowValidationIssue,
} from "../schema";

export type FlowGramMicroflowNodeType = MicroflowObjectKind;

export interface FlowGramMicroflowNodeData {
  objectId: string;
  objectKind: MicroflowObjectKind;
  actionKind?: MicroflowActionKind;
  title: string;
  subtitle?: string;
  documentation?: string;
  officialType: string;
  disabled: boolean;
  validationState: "valid" | "warning" | "error";
  runtimeState?: "idle" | "visited" | "running" | "failed" | "skipped";
  issueCount: number;
}

export interface FlowGramMicroflowEdgeData {
  flowId: string;
  flowKind: MicroflowFlow["kind"];
  edgeKind: NonNullable<MicroflowFlow["editor"]["edgeKind"]>;
  isErrorHandler: boolean;
  caseValues: MicroflowCaseValue[];
  label?: string;
  description?: string;
  runtimeState?: "idle" | "visited" | "failed" | "skipped";
  validationState: "valid" | "warning" | "error";
}

export interface FlowGramMicroflowSelection {
  objectId?: string;
  flowId?: string;
}

export interface FlowGramMicroflowChangeReason {
  kind:
    | "flowgramNodeMove"
    | "flowgramNodeAdd"
    | "flowgramNodeDelete"
    | "flowgramLineAdd"
    | "flowgramLineDelete"
    | "flowgramSelection"
    | "flowgramReload";
}

export interface FlowGramMicroflowPendingLine {
  sourcePortId: string;
  targetPortId: string;
  sourceObjectId: string;
  targetObjectId: string;
  position?: MicroflowPoint;
}

export type FlowGramMicroflowIssueIndex = Map<string, MicroflowValidationIssue[]>;

