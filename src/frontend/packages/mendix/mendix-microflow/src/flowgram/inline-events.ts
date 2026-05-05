export const MICROFLOW_INLINE_NODE_TOGGLE_EVENT = "atlas:microflow-inline-node-toggle";
export const MICROFLOW_INLINE_NODE_INSPECT_EVENT = "atlas:microflow-inline-node-inspect";
export const MICROFLOW_INLINE_FIELD_COMMIT_EVENT = "atlas:microflow-inline-field-commit";
export const MICROFLOW_INLINE_QUICK_FIX_EVENT = "atlas:microflow-inline-quick-fix-apply";
export const MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT = "atlas:microflow-inline-line-label-commit";

export interface MicroflowInlineNodeToggleDetail {
  nodeId?: string;
  runtimeNodeId?: string;
  expanded?: boolean;
}

export interface MicroflowInlineNodeInspectDetail {
  nodeId?: string;
  runtimeNodeId?: string;
  inspect?: "runtime" | "error";
}

export interface MicroflowInlineFieldCommitDetail {
  nodeId: string;
  fieldPath: string;
  value: string;
  editType: string;
}

export interface MicroflowInlineLineLabelCommitDetail {
  edgeId?: string;
  flowId?: string;
  value?: string;
}

export interface MicroflowInlineQuickFixDetail {
  nodeId: string;
  suggestionId?: string;
  actionKind: string;
  fieldPath?: string;
  value?: unknown;
  editType?: string;
}

function dispatchInlineEvent<T>(name: string, detail: T): void {
  window.dispatchEvent(new CustomEvent(name, { detail }));
}

export function emitInlineNodeToggle(detail: MicroflowInlineNodeToggleDetail): void {
  dispatchInlineEvent(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, detail);
}

export function emitInlineNodeInspect(detail: MicroflowInlineNodeInspectDetail): void {
  dispatchInlineEvent(MICROFLOW_INLINE_NODE_INSPECT_EVENT, detail);
}

export function emitInlineFieldCommit(detail: MicroflowInlineFieldCommitDetail): void {
  dispatchInlineEvent(MICROFLOW_INLINE_FIELD_COMMIT_EVENT, detail);
}

export function emitInlineLineLabelCommit(detail: MicroflowInlineLineLabelCommitDetail): void {
  dispatchInlineEvent(MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT, detail);
}

export function emitInlineQuickFix(detail: MicroflowInlineQuickFixDetail): void {
  dispatchInlineEvent(MICROFLOW_INLINE_QUICK_FIX_EVENT, detail);
}
