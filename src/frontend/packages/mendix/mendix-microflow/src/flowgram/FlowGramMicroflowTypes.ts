import type {
  MicroflowNodeAvailability,
  MicroflowActionKind,
  MicroflowAction,
  MicroflowCaseValue,
  MicroflowFlow,
  MicroflowObjectKind,
  MicroflowPoint,
  MicroflowLoopedActivity,
  MicroflowValidationIssue,
} from "../schema";
import type { MicroflowCaseEditorKind } from "./adapters/flowgram-case-options";

export type FlowGramMicroflowNodeType = MicroflowObjectKind;

export interface FlowGramMicroflowNodeData {
  objectId: string;
  objectKind: MicroflowObjectKind;
  collectionId: string;
  parentObjectId?: string;
  loopSource?: MicroflowLoopedActivity["loopSource"];
  iteratorVariableName?: string;
  listVariableName?: string;
  currentIndexVariableName?: "$currentIndex";
  loopSummary?: {
    childCount: number;
    flowCount: number;
    nestedLoopCount: number;
    actionCount: number;
    eventCount: number;
    annotationCount: number;
  };
  actionKind?: MicroflowActionKind;
  action?: MicroflowAction;
  availability?: MicroflowNodeAvailability;
  availabilityReason?: string;
  title: string;
  subtitle?: string;
  documentation?: string;
  officialType: string;
  disabled: boolean;
  validationState: "valid" | "warning" | "error";
  runtimeState?: "idle" | "success" | "visited" | "running" | "failed" | "skipped";
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
  runtimeState?: "idle" | "visited" | "failed" | "skipped" | "errorHandlerVisited" | "selectedCase";
  validationState: "valid" | "warning" | "error";
}

export interface FlowGramMicroflowSelection {
  objectId?: string;
  flowId?: string;
  collectionId?: string;
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
  caseKind: MicroflowCaseEditorKind;
  sourcePortId: string;
  targetPortId: string;
  sourceObjectId: string;
  targetObjectId: string;
  position?: MicroflowPoint;
}

export type FlowGramMicroflowIssueIndex = Map<string, MicroflowValidationIssue[]>;
