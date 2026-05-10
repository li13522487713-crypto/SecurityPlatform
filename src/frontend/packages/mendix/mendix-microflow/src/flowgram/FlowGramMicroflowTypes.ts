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
import type { MicroflowRuntimeValueGroup } from "../debug/runtime-value-view-model";
import type { MicroflowCaseEditorKind } from "./adapters/flowgram-case-options";

export type FlowGramMicroflowNodeType = MicroflowObjectKind;

export type MicroflowOutputMappingSource = "variable" | "constant" | "expression";

export interface MicroflowOutputMapping {
  key: string;
  source: MicroflowOutputMappingSource;
  variableName?: string;
  constantValue?: unknown;
  expression?: string;
}

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
  outputMappings?: MicroflowOutputMapping[];
  availability?: MicroflowNodeAvailability;
  availabilityReason?: string;
  title: string;
  subtitle?: string;
  inlineConfig?: MicroflowNodeInlineConfig;
  documentation?: string;
  officialType: string;
  disabled: boolean;
  validationState: "valid" | "warning" | "error";
  runtimeState?: "idle" | "success" | "visited" | "running" | "failed" | "skipped" | "unsupported";
  runtimeErrorCode?: string;
  runtimeErrorMessage?: string;
  issueCount: number;
}

export type MicroflowNodeViewMode =
  | "compact"
  | "expanded"
  | "editing"
  | "running"
  | "inspectingError"
  | "inspectingRuntime";

export type MicroflowInlineEditType =
  | "text"
  | "select"
  | "variable"
  | "expression"
  | "condition"
  | "http"
  | "assignment"
  | "branch"
  | "json"
  | "mapping"
  | "approval"
  | "loop"
  | "outputMappings";

export interface MicroflowInlineEditableField {
  id: string;
  label: string;
  value: string;
  displayValue?: string;
  fieldPath: string;
  editType: MicroflowInlineEditType;
  required?: boolean;
  readonly?: boolean;
  invalid?: boolean;
  errorMessage?: string;
  placeholder?: string;
  options?: Array<{ label: string; value: string }>;
}

export interface MicroflowInlineSection {
  id: string;
  title: string;
  kind:
    | "inputs"
    | "outputs"
    | "conditions"
    | "branches"
    | "variables"
    | "http"
    | "approval"
    | "loop"
    | "runtime"
    | "errors"
    | "advanced";
  collapsed?: boolean;
  maxVisibleRows?: number;
  fields: MicroflowInlineEditableField[];
}

export interface MicroflowNodeMiniSummaryLine {
  id: string;
  kind: "input" | "output" | "assignment" | "condition" | "branch" | "http" | "runtime" | "error" | "text";
  label?: string;
  value: string;
  fieldPath?: string;
  editable?: boolean;
  error?: boolean;
  warning?: boolean;
}

export interface MicroflowNodeRuntimeInlineState {
  visited?: boolean;
  running?: boolean;
  success?: boolean;
  failed?: boolean;
  skipped?: boolean;
  durationMs?: number;
  executionIndex?: number;
  inputCount?: number;
  outputCount?: number;
  selectedBranchLabel?: string;
  inputPreview?: string;
  outputPreview?: string;
  outputSummaries?: string[];
  inputGroup?: MicroflowRuntimeValueGroup;
  outputGroup?: MicroflowRuntimeValueGroup;
  variableGroup?: MicroflowRuntimeValueGroup;
  rawTraceJson?: string;
  variableSnapshot?: Array<{ name: string; type?: string; valuePreview: string }>;
  error?: {
    code?: string;
    message: string;
    stackPreview?: string;
    fixSuggestions?: Array<{
      id: string;
      label: string;
      actionKind: string;
      fieldPath?: string;
      value?: unknown;
      editType?: MicroflowInlineEditType;
    }>;
  };
}

export interface MicroflowNodeInlineConfig {
  viewMode: MicroflowNodeViewMode;
  summaryLines: Array<{
    id: string;
    label?: string;
    value: string;
    kind?: "input" | "output" | "assignment" | "condition" | "http" | "branch" | "approval" | "loop" | "runtime" | "error" | "text";
    fieldPath?: string;
    editable?: boolean;
    warning?: boolean;
    error?: boolean;
  }>;
  sections: MicroflowInlineSection[];
  runtime?: MicroflowNodeRuntimeInlineState;
}

export interface FlowGramMicroflowEdgeData {
  flowId: string;
  flowKind: MicroflowFlow["kind"];
  edgeKind: NonNullable<MicroflowFlow["editor"]["edgeKind"]>;
  isErrorHandler: boolean;
  caseValues: MicroflowCaseValue[];
  label?: string;
  description?: string;
  branchOrder?: number;
  showInExport?: boolean;
  runtimeState?: "idle" | "visited" | "running" | "failed" | "skipped" | "errorHandlerVisited" | "selectedCase";
  sourceNodeId?: string;
  sourceObjectKind?: MicroflowObjectKind;
  sourceActionKind?: MicroflowActionKind;
  sourcePortId?: string;
  targetNodeId?: string;
  targetObjectKind?: MicroflowObjectKind;
  targetActionKind?: MicroflowActionKind;
  targetPortId?: string;
  validationState: "valid" | "warning" | "error";
}

export interface FlowGramMicroflowSelection {
  objectId?: string;
  flowId?: string;
  collectionId?: string;
  objectIds?: string[];
  flowIds?: string[];
  mode?: "none" | "single" | "multi";
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

import { createContext } from "react";
export const MicroflowNodeViewModesContext = createContext<Record<string, MicroflowNodeViewMode>>({});
