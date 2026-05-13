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

type InlineEventListener<T> = (detail: T) => void;

const inlineNodeToggleListeners = new Set<InlineEventListener<MicroflowInlineNodeToggleDetail>>();
const inlineNodeInspectListeners = new Set<InlineEventListener<MicroflowInlineNodeInspectDetail>>();

function dispatchInlineEvent<T>(name: string, detail: T): void {
  if (typeof window === "undefined") {
    return;
  }
  window.dispatchEvent(new CustomEvent(name, { detail }));
}

function notifyInlineListeners<T>(listeners: Set<InlineEventListener<T>>, detail: T): void {
  for (const listener of listeners) {
    listener(detail);
  }
}

export function subscribeInlineNodeToggle(listener: InlineEventListener<MicroflowInlineNodeToggleDetail>): () => void {
  inlineNodeToggleListeners.add(listener);
  return () => {
    inlineNodeToggleListeners.delete(listener);
  };
}

export function subscribeInlineNodeInspect(listener: InlineEventListener<MicroflowInlineNodeInspectDetail>): () => void {
  inlineNodeInspectListeners.add(listener);
  return () => {
    inlineNodeInspectListeners.delete(listener);
  };
}

export function emitInlineNodeToggle(detail: MicroflowInlineNodeToggleDetail): void {
  notifyInlineListeners(inlineNodeToggleListeners, detail);
  dispatchInlineEvent(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, detail);
}

export function emitInlineNodeInspect(detail: MicroflowInlineNodeInspectDetail): void {
  notifyInlineListeners(inlineNodeInspectListeners, detail);
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
