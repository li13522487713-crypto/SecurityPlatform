import { useCallback, useEffect, useImperativeHandle, useLayoutEffect, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent as ReactKeyboardEvent, type ReactNode, type Ref } from "react";

import { Badge, Button, Empty, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconDelete, IconPlay, IconRefresh, IconSave, IconSetting, IconTickCircle, IconUndo, IconRedo } from "@douyinfe/semi-icons";

import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel } from "../property-panel";
import type { MicroflowApiClient, SaveMicroflowResponse, TestRunMicroflowResponse, ValidateMicroflowResponse } from "../runtime-adapter";
import { MicroflowTestRunModal } from "../debug/MicroflowTestRunModal";
import type { MicroflowRunSession, MicroflowTestRunInput, MicroflowTestRunSample, MicroflowTraceFrame } from "../debug/trace-types";
import { readStoredTestRunSamples, writeStoredTestRunSamples } from "../debug/test-run-samples-store";
import type { MicroflowValidationAdapterLike, MicroflowValidationMode } from "../performance";
import type { MicroflowMetadataAdapter, MicroflowMetadataCatalog } from "../metadata";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData, FlowGramMicroflowSelection } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowNodeViewMode } from "../flowgram/FlowGramMicroflowTypes";
import {
  FlowGramMicroflowNativeCanvas,
} from "../flowgram/FlowGramMicroflowNativeCanvas";
import {
  createWorkflowNodeFromPanelItem,
  workflowEdgeById,
  workflowEdgeCount,
  workflowNodeById,
  workflowNodeCount,
} from "../flowgram/flowgram-native-schema";
import {
  MICROFLOW_INLINE_FIELD_COMMIT_EVENT,
  MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT,
  MICROFLOW_INLINE_NODE_INSPECT_EVENT,
  MICROFLOW_INLINE_NODE_TOGGLE_EVENT,
  MICROFLOW_INLINE_QUICK_FIX_EVENT,
  type MicroflowInlineFieldCommitDetail,
  type MicroflowInlineLineLabelCommitDetail,
  type MicroflowInlineNodeInspectDetail,
  type MicroflowInlineNodeToggleDetail,
  type MicroflowInlineQuickFixDetail,
} from "../flowgram/inline-events";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { createDefaultEditorState } from "../schema/utils/schema-utils";
import type {
  MicroflowEditorHandle,
  MicroflowEditorLabels,
  MicroflowEditorStatusSnapshot,
  MicroflowWorkbenchBottomTab,
  MicroflowWorkbenchLayoutState,
  MicroflowWorkbenchStatus,
} from "./index";
import { buildAcceptance120Schema as buildAcceptance120SchemaFixture } from "./acceptance-120-fixture";

const { Text, Title } = Typography;

const RAIL_WIDTH_PX = 44;
const LEFT_PANEL_EXPANDED_PX = 300;
const RIGHT_PANEL_EXPANDED_PX = 360;
const BOTTOM_STRIP_HEIGHT_PX = 32;
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;
const LEGACY_PROPERTY_PANEL_ENABLED = false;
const LEGACY_BOTTOM_PANEL_ENABLED = false;

type BottomDockMode = "collapsed" | "peek" | "full";
type NativeHistoryReason = "init" | "workflow" | "property" | "selection" | "layout" | "delete" | "runtime";
type NativeContextMenuState = {
  x: number;
  y: number;
  selection: FlowGramMicroflowSelection;
};

export interface NativeMicroflowEditorProps {
  schema: MicroflowDesignSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
  nodePanelLabels?: Partial<MicroflowNodePanelLabels>;
  readonly?: boolean;
  onPublish?: (schema: MicroflowDesignSchema) => Promise<void> | void;
  onSaveComplete?: (response: SaveMicroflowResponse) => void;
  onValidateComplete?: (response: ValidateMicroflowResponse) => void;
  onValidationStateChange?: (state: { microflowId: string; issues: MicroflowValidationIssue[]; status: string; lastValidatedAt?: Date }) => void;
  onTestRunComplete?: (response: TestRunMicroflowResponse) => void;
  onSchemaChange?: (schema: MicroflowDesignSchema) => void;
  metadataAdapter?: MicroflowMetadataAdapter;
  metadataCatalog?: MicroflowMetadataCatalog;
  metadataWorkspaceId?: string;
  metadataModuleId?: string;
  validationAdapter?: MicroflowValidationAdapterLike;
  toolbarMode?: "internal" | "external";
  editorRef?: Ref<MicroflowEditorHandle>;
  shellMode?: "legacy-host-layout" | "editor-native-layout";
  onLayoutStateChange?: (state: MicroflowWorkbenchLayoutState) => void;
  onWorkbenchStatusChange?: (status: MicroflowWorkbenchStatus) => void;
}

const defaultLabels: Partial<MicroflowEditorLabels> = {
  save: "保存",
  validate: "校验",
  testRun: "运行",
  publish: "发布",
  undo: "撤销",
  redo: "重做",
  nodePanel: "Nodes",
  properties: "Properties",
  problems: "Problems",
  debug: "Debug",
  quickFix: "Quick fix",
  quickFixUnavailable: "Quick fix is not available for this issue.",
  missingDecisionBranchCreated: "Missing Decision branch created.",
  format: "Auto",
};

const NODE_STATUS_LEGEND: Array<{ key: string; label: string; description: string; tone?: "active" | "success" | "error" | "muted" }> = [
  { key: "idle", label: "默认态", description: "紧凑摘要，只读" },
  { key: "hover", label: "悬停态", description: "显示编辑入口" },
  { key: "selected", label: "选中态", description: "蓝色描边", tone: "active" },
  { key: "expanded", label: "展开态", description: "关键输入输出编辑" },
  { key: "running", label: "运行中", description: "显示运行状态", tone: "active" },
  { key: "success", label: "运行成功", description: "显示耗时与成功", tone: "success" },
  { key: "failed", label: "运行失败", description: "显示错误摘要", tone: "error" },
  { key: "skipped", label: "跳过", description: "降低透明度", tone: "muted" },
];

function cloneSchema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const cloned = JSON.parse(JSON.stringify(schema)) as MicroflowDesignSchema;
  const defaultEditor = createDefaultEditorState();
  return {
    ...cloned,
    workflow: {
      ...(cloned.workflow ?? {}),
      nodes: Array.isArray(cloned.workflow?.nodes) ? cloned.workflow.nodes : [],
      edges: Array.isArray(cloned.workflow?.edges) ? cloned.workflow.edges : [],
    },
    editor: {
      ...defaultEditor,
      ...(cloned.editor ?? {}),
      viewport: {
        ...defaultEditor.viewport,
        ...(cloned.editor?.viewport ?? {}),
      },
      selection: {
        ...(cloned.editor?.selection ?? {}),
      },
    },
    parameters: Array.isArray(cloned.parameters) ? cloned.parameters : [],
    variables: Array.isArray(cloned.variables) ? cloned.variables : [],
    validation: cloned.validation ?? { issues: [] },
  };
}

function isEditableElement(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
}

function schemaWorkflowSignature(schema: MicroflowDesignSchema): string {
  return JSON.stringify(schema.workflow);
}

function issue(id: string, message: string, code: string, severity: MicroflowValidationIssue["severity"] = "error", extra: Partial<MicroflowValidationIssue> = {}): MicroflowValidationIssue {
  return {
    id,
    severity,
    message,
    code,
    source: "schema",
    blockSave: severity === "error",
    blockPublish: severity === "error",
    createdAt: new Date().toISOString(),
    ...extra,
  };
}

function validateNativeSchema(schema: MicroflowDesignSchema, mode: MicroflowValidationMode = "edit"): MicroflowValidationIssue[] {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  const edges = schema.workflow.edges as MicroflowWorkflowEdgeJSON[];
  const issues: MicroflowValidationIssue[] = [];
  const starts = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "startEvent");
  const ends = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "endEvent");
  if (starts.length !== 1) {
    issues.push(issue(`${schema.id}:start-count`, "微流必须且只能有一个 Start 节点。", "MF_START_COUNT"));
  }
  if (ends.length < 1) {
    issues.push(issue(`${schema.id}:end-missing`, "微流至少需要一个 End 节点。", "MF_END_MISSING"));
  }
  const nodeIds = new Set(nodes.map(node => node.id));
  const edgeKeys = new Set<string>();
  for (const edge of edges) {
    const edgeKey = [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].join("::");
    if (edgeKeys.has(edgeKey)) {
      issues.push(issue(`${schema.id}:duplicate-edge:${edgeKey}`, "不允许重复连线。", "MF_EDGE_DUPLICATED", "error", { flowId: edge.id }));
    }
    edgeKeys.add(edgeKey);
    if (!nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) {
      issues.push(issue(`${schema.id}:edge-missing-node:${edge.id}`, "连线引用了不存在的节点。", "MF_EDGE_NODE_MISSING", "error", { flowId: edge.id }));
    }
    if (edge.sourceNodeID === edge.targetNodeID) {
      issues.push(issue(`${schema.id}:edge-self:${edge.id}`, "不允许节点自连。", "MF_EDGE_SELF_LOOP", "error", { flowId: edge.id }));
    }
  }
  if (mode !== "edit" && starts[0] && edges.every(edge => edge.sourceNodeID !== starts[0].id)) {
    issues.push(issue(`${schema.id}:start-no-outgoing`, "Start 节点需要至少一条出线。", "MF_START_NO_OUTGOING", "error", { objectId: starts[0].id }));
  }
  return issues;
}

function summarizeIssues(issues: MicroflowValidationIssue[]) {
  return {
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    infoCount: issues.filter(item => item.severity === "info").length,
  };
}

function selectionPatch(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): MicroflowDesignSchema {
  return {
    ...schema,
    editor: {
      ...schema.editor,
      selectedObjectId: selection.objectId,
      selectedFlowId: selection.flowId,
      selectedCollectionId: selection.collectionId,
      selection: {
        objectId: selection.objectId,
        flowId: selection.flowId,
        collectionId: selection.collectionId,
        objectIds: selection.objectIds,
        flowIds: selection.flowIds,
        mode: selection.mode,
      },
    },
  };
}

function isProtectedWorkflowNode(node: MicroflowWorkflowNodeJSON): boolean {
  const kind = (node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type;
  return node.id === "start" || node.id === "end" || kind === "startEvent" || kind === "endEvent";
}

function selectionHasOnlyProtectedObjects(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): boolean {
  const selectedObjects = [selection.objectId, ...(selection.objectIds ?? [])].filter((id): id is string => Boolean(id));
  if (!selectedObjects.length) {
    return false;
  }
  return selectedObjects.every(objectId => {
    const node = workflowNodeById(schema.workflow, objectId) as MicroflowWorkflowNodeJSON | undefined;
    return node ? isProtectedWorkflowNode(node) : false;
  });
}

function normalizeSelectionIds(ids?: string[]): string[] {
  if (!Array.isArray(ids) || ids.length === 0) {
    return [];
  }
  return [...new Set(ids.filter((id): id is string => Boolean(id)))].sort((a, b) => a.localeCompare(b));
}

function isSameSelection(a: FlowGramMicroflowSelection | undefined, b: FlowGramMicroflowSelection | undefined): boolean {
  if (!a && !b) {
    return true;
  }
  if (!a || !b) {
    return false;
  }
  return (
    a.objectId === b.objectId
    && a.flowId === b.flowId
    && a.collectionId === b.collectionId
    && a.mode === b.mode
    && JSON.stringify(normalizeSelectionIds(a.objectIds)) === JSON.stringify(normalizeSelectionIds(b.objectIds))
    && JSON.stringify(normalizeSelectionIds(a.flowIds)) === JSON.stringify(normalizeSelectionIds(b.flowIds))
  );
}

function deleteSelection(schema: MicroflowDesignSchema, targetSelection?: FlowGramMicroflowSelection): MicroflowDesignSchema {
  const selection = targetSelection ?? schema.editor.selection;
  const selectedObjects = new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)));
  const selectedFlows = new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id)));
  const protectedObjects = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && isProtectedWorkflowNode(node))
      .map(node => node.id),
  );
  const deletedParameterIds = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && !protectedObjects.has(node.id))
      .flatMap(node => {
        const data = node.data as (Partial<FlowGramMicroflowNodeData> & {
          parameterId?: string;
        }) | undefined;
        const parameterId = data?.parameterId;
        return data?.objectKind === "parameterObject"
          ? [parameterId].filter((id): id is string => Boolean(id))
          : [];
      }),
  );
  return {
    ...schema,
    parameters: deletedParameterIds.size > 0
      ? schema.parameters.filter(parameter => !deletedParameterIds.has(parameter.id))
      : schema.parameters,
    workflow: {
      ...schema.workflow,
      nodes: (schema.workflow.nodes as MicroflowWorkflowNodeJSON[]).filter(node => !selectedObjects.has(node.id) || protectedObjects.has(node.id)),
      edges: (schema.workflow.edges as MicroflowWorkflowEdgeJSON[]).filter(edge => {
        const id = (edge.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? edge.id;
        return !selectedFlows.has(String(id))
          && (!selectedObjects.has(edge.sourceNodeID) || protectedObjects.has(edge.sourceNodeID))
          && (!selectedObjects.has(edge.targetNodeID) || protectedObjects.has(edge.targetNodeID));
      }),
    },
    editor: {
      ...schema.editor,
      selectedObjectId: undefined,
      selectedFlowId: undefined,
      selection: {},
    },
  };
}

function applyAutoLayout(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: nodes.map((node, index) => ({
        ...node,
        meta: {
          ...node.meta,
          position: {
            x: 260 + index * 260,
            y: 220 + (index % 2) * 120,
          },
        },
      })),
    },
  };
}

function panelStyle(): CSSProperties {
  return {
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-2, #fff)",
  };
}

function propertyPaneStyle(): CSSProperties {
  return {
    width: RIGHT_PANEL_EXPANDED_PX,
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-1, #fff)",
  };
}

function contextMenuPosition(point: { x: number; y: number }): { left: number; top: number } {
  const menuWidth = 168;
  const menuHeight = 128;
  const margin = 8;
  if (typeof window === "undefined") {
    return { left: point.x, top: point.y };
  }
  return {
    left: Math.max(margin, Math.min(point.x, window.innerWidth - menuWidth - margin)),
    top: Math.max(margin, Math.min(point.y, window.innerHeight - menuHeight - margin)),
  };
}

function resolveInlineNodeViewModeAliases(
  schema: MicroflowDesignSchema,
  nodeId?: string,
  runtimeNodeId?: string,
): string[] {
  const seeds = [nodeId, runtimeNodeId]
    .filter((item): item is string => Boolean(item))
    .map(item => item.trim())
    .filter(Boolean);
  if (!seeds.length) {
    return [];
  }
  const aliases = new Set<string>();
  for (const seed of seeds) {
    aliases.add(seed);
    aliases.add(seed.startsWith("node-") ? seed.slice("node-".length) : `node-${seed}`);
  }
  const nodes = (schema.workflow.nodes as MicroflowWorkflowNodeJSON[] | undefined) ?? [];
  for (const node of nodes) {
    const data = (node.data as Partial<FlowGramMicroflowNodeData> | undefined) ?? {};
    const candidates = [node.id, data.objectId].filter((item): item is string => Boolean(item));
    const matched = candidates.some(candidate => {
      const normalized = candidate.startsWith("node-") ? candidate.slice("node-".length) : candidate;
      return aliases.has(candidate) || aliases.has(normalized) || aliases.has(`node-${normalized}`);
    });
    if (!matched) {
      continue;
    }
    aliases.add(node.id);
    aliases.add(`node-${node.id}`);
    if (data.objectId) {
      aliases.add(data.objectId);
      aliases.add(`node-${data.objectId}`);
    }
  }
  return [...aliases];
}

function normalizeWorkflowCompatShape(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const nodes = (schema.workflow.nodes as MicroflowWorkflowNodeJSON[]).map(node => {
    const record = node as unknown as Record<string, unknown>;
    const data = (node.data ?? {}) as Record<string, unknown>;
    const dataAction = data.action as Record<string, unknown> | undefined;
    const rootAction = record.action as Record<string, unknown> | undefined;
    if (dataAction && rootAction) {
      const merged = {
        ...rootAction,
        ...dataAction,
      } as Record<string, unknown>;
      record.action = JSON.parse(JSON.stringify(merged)) as Record<string, unknown>;
      node.data = {
        ...data,
        action: JSON.parse(JSON.stringify(merged)) as Record<string, unknown>,
      };
    } else if (dataAction) {
      record.action = JSON.parse(JSON.stringify(dataAction)) as Record<string, unknown>;
    } else if (rootAction) {
      node.data = {
        ...data,
        action: JSON.parse(JSON.stringify(rootAction)) as Record<string, unknown>,
      };
    }
    return node;
  });
  const edges = (schema.workflow.edges as MicroflowWorkflowEdgeJSON[]).map(edge => {
    const edgeRecord = edge as unknown as Record<string, unknown>;
    const data = (edge.data ?? {}) as Record<string, unknown>;
    if (Array.isArray(data.caseValues)) {
      edgeRecord.caseValues = data.caseValues;
    } else if (Array.isArray(edge.caseValues)) {
      data.caseValues = edge.caseValues as unknown[];
      edge.data = data as Record<string, unknown>;
    }
    return edge;
  });
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes,
      edges,
    },
  };
}

function setByPath(root: unknown, path: string, value: unknown): unknown {
  const segments = path.split(".").filter(Boolean);
  if (segments.length === 0) {
    return root;
  }
  const clone = Array.isArray(root) ? [...root] : { ...(root as Record<string, unknown>) };
  let cursor: Record<string, unknown> | unknown[] = clone as Record<string, unknown>;
  for (let index = 0; index < segments.length - 1; index += 1) {
    const key = segments[index]!;
    const next = (cursor as Record<string, unknown>)[key];
    const nextClone = Array.isArray(next) ? [...next] : { ...((next as Record<string, unknown>) ?? {}) };
    (cursor as Record<string, unknown>)[key] = nextClone;
    cursor = nextClone as Record<string, unknown>;
  }
  (cursor as Record<string, unknown>)[segments[segments.length - 1]!] = value;
  return clone;
}

function applyNodeFieldPatch(node: MicroflowWorkflowNodeJSON, fieldPath: string, value: unknown): void {
  const normalizedPath = fieldPath.startsWith("data.") ? fieldPath.slice("data.".length) : fieldPath;
  const firstSegment = normalizedPath.split(".").filter(Boolean)[0];
  const actionRootRecord = ((node.data as Record<string, unknown> | undefined)?.action ?? {}) as Record<string, unknown>;
  const actionRootKeys = new Set(Object.keys(actionRootRecord));
  if (normalizedPath.startsWith("action.")) {
    const actionPath = normalizedPath.slice("action.".length);
    const dataRecord = ((node.data ?? {}) as Record<string, unknown>);
    const nextAction = setByPath((dataRecord.action ?? {}) as Record<string, unknown>, actionPath, value) as Record<string, unknown>;
    node.data = {
      ...dataRecord,
      action: nextAction,
    };
    (node as unknown as Record<string, unknown>).action = nextAction;
    return;
  }
  if (firstSegment && actionRootKeys.has(firstSegment)) {
    const dataRecord = ((node.data ?? {}) as Record<string, unknown>);
    const nextAction = setByPath((dataRecord.action ?? {}) as Record<string, unknown>, normalizedPath, value) as Record<string, unknown>;
    node.data = {
      ...dataRecord,
      action: nextAction,
    };
    (node as unknown as Record<string, unknown>).action = nextAction;
    return;
  }
  const fullPatchedNode = setByPath(node as unknown as Record<string, unknown>, fieldPath, value) as Record<string, unknown>;
  Object.assign(node as unknown as Record<string, unknown>, fullPatchedNode);
  const nextData = setByPath((node.data ?? {}) as Record<string, unknown>, normalizedPath, value) as Record<string, unknown>;
  node.data = nextData;
}

function isInlineFieldPathAllowed(path: string): boolean {
  if (path.includes("notExists")) {
    return false;
  }
  if (path.startsWith("edge:")) {
    return /^edge:[^.\s]+\.data\.(label|branchLabel)$/.test(path);
  }
  return true;
}

function normalizeDecisionLabel(value: string): string | undefined {
  const normalized = value.trim().toLowerCase();
  if (normalized === "true" || normalized === "false" || normalized === "else") {
    return normalized;
  }
  return undefined;
}

function branchKeyFromSourcePort(sourcePort?: string): string | undefined {
  if (!sourcePort) {
    return undefined;
  }
  const [, key] = sourcePort.split(":");
  return key || undefined;
}

function canonicalBranchLabel(sourcePortId: string | undefined, value: string): string | undefined {
  const normalized = value.trim().toLowerCase();
  if (!normalized) {
    return undefined;
  }
  if ((sourcePortId ?? "").startsWith("decision:")) {
    return normalizeDecisionLabel(normalized);
  }
  if ((sourcePortId ?? "").startsWith("approval:") && ["approved", "rejected", "timeout"].includes(normalized)) {
    return normalized;
  }
  if ((sourcePortId ?? "").startsWith("loop:") && ["body", "done", "break", "continue"].includes(normalized)) {
    return normalized;
  }
  if ((sourcePortId ?? "").startsWith("error:") && ["error", "fallback", "rethrow", "handled"].includes(normalized)) {
    return normalized;
  }
  return undefined;
}

function normalizeLineLabelByEdgeKind(edgeKind: FlowGramMicroflowEdgeData["edgeKind"], sourcePortId: string | undefined, value: string): string | undefined {
  const trimmed = value.trim();
  const normalized = value.trim().toLowerCase();
  if (!normalized) {
    return undefined;
  }
  if (edgeKind === "decisionCondition") {
    return normalizeDecisionLabel(normalized) ?? trimmed;
  }
  if ((sourcePortId ?? "").startsWith("approval:")) {
    return ["approved", "rejected", "timeout"].includes(normalized) ? normalized : trimmed;
  }
  if ((sourcePortId ?? "").startsWith("loop:")) {
    return ["body", "done", "break", "continue"].includes(normalized) ? normalized : trimmed;
  }
  if (edgeKind === "errorHandler") {
    return ["error", "fallback", "rethrow", "handled"].includes(normalized) ? normalized : trimmed;
  }
  return canonicalBranchLabel(sourcePortId, normalized) ?? trimmed;
}

type InlineFieldCommitDetail = Partial<MicroflowInlineFieldCommitDetail>;
type InlineQuickFixDetail = Partial<MicroflowInlineQuickFixDetail> & { branchValue?: unknown };

function parseKeyValueLines(value: string): Array<{ key: string; valueExpression: { raw: string } }> {
  return value
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => {
      const index = line.indexOf("=");
      if (index < 0) {
        return { key: line.trim(), valueExpression: { raw: "" } };
      }
      return {
        key: line.slice(0, index).trim(),
        valueExpression: { raw: line.slice(index + 1).trim() },
      };
    })
    .filter(item => item.key.length > 0);
}

function parseNamedExpressionLines(value: string): Array<{ name: string; valueExpression: { raw: string } }> {
  return value
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => {
      const index = line.indexOf("=");
      if (index < 0) {
        return { name: line.trim(), valueExpression: { raw: "" } };
      }
      return {
        name: line.slice(0, index).trim(),
        valueExpression: { raw: line.slice(index + 1).trim() },
      };
    })
    .filter(item => item.name.length > 0);
}

function normalizeInlineCommitValue(fieldPath: string, value: string): unknown {
  const trimmed = value.trim();
  if (fieldPath === "data.action.request.queryParameters" || fieldPath === "data.action.request.headers") {
    return parseKeyValueLines(value);
  }
  if (
    fieldPath === "data.action.argumentMappings"
    || fieldPath === "data.action.parameterMappings"
    || fieldPath === "data.action.inputMappings"
  ) {
    return parseNamedExpressionLines(value);
  }
  if (fieldPath === "data.action.request.timeoutMs") {
    const numeric = Number(value);
    return Number.isFinite(numeric) ? numeric : value;
  }
  if (trimmed === "true") return true;
  if (trimmed === "false") return false;
  if (/^-?\d+(\.\d+)?$/.test(trimmed)) {
    const numeric = Number(trimmed);
    if (Number.isFinite(numeric)) return numeric;
  }
  if ((trimmed.startsWith("{") && trimmed.endsWith("}")) || (trimmed.startsWith("[") && trimmed.endsWith("]"))) {
    try {
      return JSON.parse(trimmed);
    } catch {
      return value;
    }
  }
  return value;
}

function syncBranchLabelToSourceNode(next: MicroflowDesignSchema, edge: MicroflowWorkflowEdgeJSON, normalized: string, sourcePortHint?: string): void {
  const sourceNode = workflowNodeById(next.workflow, edge.sourceNodeID) as MicroflowWorkflowNodeJSON | undefined;
  if (!sourceNode) {
    return;
  }
  const sourcePort = sourcePortHint ?? edge.sourcePortID ?? "";
  let branchKey: string | undefined;
  if (sourcePort.startsWith("decision:")) {
    branchKey = branchKeyFromSourcePort(sourcePort);
  } else if (sourcePort.startsWith("approval:")) {
    branchKey = branchKeyFromSourcePort(sourcePort);
  } else if (sourcePort.startsWith("loop:")) {
    branchKey = branchKeyFromSourcePort(sourcePort);
  } else if (sourcePort.startsWith("error:") || ((edge.data as FlowGramMicroflowEdgeData | undefined)?.edgeKind === "errorHandler")) {
    branchKey = branchKeyFromSourcePort(sourcePort) ?? normalized;
  }
  if (!branchKey) {
    return;
  }
  const dataRecord = (sourceNode.data ?? {}) as Record<string, unknown>;
  const branchLabels = ((dataRecord.branchLabels as Record<string, unknown> | undefined) ?? {});
  sourceNode.data = {
    ...dataRecord,
    branchLabels: {
      ...branchLabels,
      [branchKey]: normalized,
    },
  };
}

export function NativeMicroflowEditor(props: NativeMicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const [schema, setSchema] = useState<MicroflowDesignSchema>(() => cloneSchema(props.schema));
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateNativeSchema(props.schema));
  const [validationStatus, setValidationStatus] = useState("idle");
  const [lastValidatedAt, setLastValidatedAt] = useState<Date>();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [testRunModalOpen, setTestRunModalOpen] = useState(false);
  const [testRunValues, setTestRunValues] = useState<Record<string, unknown>>();
  const [testRunSamples, setTestRunSamples] = useState<MicroflowTestRunSample[]>(() => readStoredTestRunSamples()[props.schema.id] ?? []);
  const [lastRunSession, setLastRunSession] = useState<MicroflowRunSession>();
  const [runtimeServiceError, setRuntimeServiceError] = useState<string>();
  const [leftOpen, setLeftOpen] = useState(true);
  const [rightOpen, setRightOpen] = useState(false);
  const [bottomDockMode, setBottomDockMode] = useState<BottomDockMode>("collapsed");
  const [bottomTab, setBottomTab] = useState<MicroflowWorkbenchBottomTab>("problems");
  const [focusObjectId, setFocusObjectId] = useState<string>();
  const [focusRequestSeq, setFocusRequestSeq] = useState(0);
  const [contextMenu, setContextMenu] = useState<NativeContextMenuState>();
  const [nodeViewModes, setNodeViewModes] = useState<Record<string, MicroflowNodeViewMode>>({});
  const [historyPast, setHistoryPast] = useState<MicroflowDesignSchema[]>([]);
  const [historyFuture, setHistoryFuture] = useState<MicroflowDesignSchema[]>([]);
  const shellRef = useRef<HTMLDivElement>(null);
  const contextMenuRef = useRef<HTMLDivElement>(null);
  const latestSchemaRef = useRef(schema);
  const savedSchemaSignatureRef = useRef(schemaWorkflowSignature(schema));
  const toolbarMode = props.toolbarMode ?? "internal";
  const shellMode = props.shellMode ?? (toolbarMode === "external" ? "editor-native-layout" : "legacy-host-layout");
  const externalLayout = shellMode === "editor-native-layout";
  const bottomOpen = bottomDockMode !== "collapsed";

  useEffect(() => {
    const next = cloneSchema(props.schema);
    const current = latestSchemaRef.current;
    const incomingSignature = schemaWorkflowSignature(next);
    const currentSignature = schemaWorkflowSignature(current);
    const schemaSwitched = next.id !== current.id;
    if (!schemaSwitched && incomingSignature === currentSignature) {
      return;
    }
    setSchema(next);
    latestSchemaRef.current = next;
    savedSchemaSignatureRef.current = schemaWorkflowSignature(next);
    setDirty(false);
    setHistoryPast([]);
    setHistoryFuture([]);
    setIssues(validateNativeSchema(next));
    setTestRunSamples(readStoredTestRunSamples()[next.id] ?? []);
    setNodeViewModes(currentModes => (schemaSwitched ? {} : currentModes));
  }, [props.schema]);

  useEffect(() => {
    const stored = readStoredTestRunSamples();
    writeStoredTestRunSamples({ ...stored, [schema.id]: testRunSamples });
  }, [schema.id, testRunSamples]);

  useLayoutEffect(() => {
    const toggleListener = (event: Event) => {
      const detail = (event as CustomEvent<MicroflowInlineNodeToggleDetail>).detail;
      if (!detail?.nodeId) {
        return;
      }
      const keys = resolveInlineNodeViewModeAliases(latestSchemaRef.current, detail.nodeId, detail.runtimeNodeId);
      if (!keys.length) {
        return;
      }
      setNodeViewModes(current => ({
        ...current,
        ...Object.fromEntries(keys.map(key => [key, detail.expanded ? "expanded" : "compact"])),
      }));
    };
    const inspectListener = (event: Event) => {
      const detail = (event as CustomEvent<MicroflowInlineNodeInspectDetail>).detail;
      if (!detail?.nodeId || !detail.inspect) {
        return;
      }
      const keys = resolveInlineNodeViewModeAliases(latestSchemaRef.current, detail.nodeId, detail.runtimeNodeId);
      if (!keys.length) {
        return;
      }
      const mode: MicroflowNodeViewMode = detail.inspect === "error" ? "inspectingError" : "inspectingRuntime";
      setNodeViewModes(current => ({
        ...current,
        ...Object.fromEntries(keys.map(key => [key, mode])),
      }));
    };
    window.addEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, toggleListener as EventListener);
    window.addEventListener(MICROFLOW_INLINE_NODE_INSPECT_EVENT, inspectListener as EventListener);
    return () => {
      window.removeEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, toggleListener as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_NODE_INSPECT_EVENT, inspectListener as EventListener);
    };
  }, []);

  const commitSchema = useCallback((next: MicroflowDesignSchema, reason: NativeHistoryReason, options: { pushHistory?: boolean; dirty?: boolean } = {}) => {
    const shouldPushHistory = options.pushHistory !== false && reason !== "selection" && reason !== "runtime";
    const normalized = normalizeWorkflowCompatShape(cloneSchema(next));
    if ((globalThis as { __VITEST__?: boolean }).__VITEST__) {
      const restNode = (normalized.workflow.nodes as MicroflowWorkflowNodeJSON[]).find(item => item.id === "rest-1");
      const restAction = ((restNode?.data as Record<string, unknown> | undefined)?.action ?? {}) as Record<string, unknown>;
      const restUrl = ((((restAction.request as Record<string, unknown> | undefined)?.urlExpression as Record<string, unknown> | undefined)?.raw) ?? "") as string;
      const decisionEdges = (normalized.workflow.edges as MicroflowWorkflowEdgeJSON[]).filter(edge => edge.sourceNodeID === "decision");
      // eslint-disable-next-line no-console
      console.info("[commit-schema]", reason, restUrl, decisionEdges.length, decisionEdges.map(edge => edge.caseValues));
    }
    latestSchemaRef.current = normalized;
    setSchema(current => {
      if (shouldPushHistory) {
        setHistoryPast(past => [...past, cloneSchema(current)].slice(-100));
        setHistoryFuture([]);
      }
      return normalized;
    });
    const nextDirty = options.dirty ?? (reason !== "selection" && reason !== "runtime");
    if (nextDirty) {
      setDirty(true);
      props.onSchemaChange?.(normalized);
    }
    const nextIssues = validateNativeSchema(normalized);
    setIssues(nextIssues);
  }, [props]);

  const applyInlineFieldCommit = useCallback((detail: InlineFieldCommitDetail) => {
    if (!detail?.nodeId || !detail.fieldPath) return false;
    if (!isInlineFieldPathAllowed(detail.fieldPath)) {
      Toast.warning("不支持的内联字段路径。");
      return false;
    }
    const next = cloneSchema(latestSchemaRef.current);
    if (detail.nodeId === "end" && (detail.fieldPath === "returnVariableName" || detail.fieldPath === "schema.returnVariableName")) {
      next.returnVariableName = detail.value ?? "";
      commitSchema(next, "property");
      return true;
    }
    const node = workflowNodeById(next.workflow, detail.nodeId) as MicroflowWorkflowNodeJSON | undefined;
    if (!node) return false;
    const normalizedValue = normalizeInlineCommitValue(detail.fieldPath, detail.value ?? "");
    applyNodeFieldPatch(node, detail.fieldPath, normalizedValue);
    if ((globalThis as { __VITEST__?: boolean }).__VITEST__) {
      const patchedAction = ((node.data as Record<string, unknown> | undefined)?.action ?? {}) as Record<string, unknown>;
      const url = ((((patchedAction.request as Record<string, unknown> | undefined)?.urlExpression as Record<string, unknown> | undefined)?.raw) ?? "") as string;
      // eslint-disable-next-line no-console
      console.info("[inline-commit]", detail.nodeId, detail.fieldPath, detail.value, url);
    }
    commitSchema(next, "property");
    return true;
  }, [commitSchema]);

  useLayoutEffect(() => {
    const onFieldCommit = (event: Event) => {
      const detail = (event as CustomEvent<InlineFieldCommitDetail>).detail;
      if (props.readonly) return;
      applyInlineFieldCommit(detail);
    };
    const onLineLabelCommit = (event: Event) => {
      const detail = (event as CustomEvent<MicroflowInlineLineLabelCommitDetail>).detail;
      if (props.readonly) return;
      const edgeKey = detail?.edgeId ?? detail?.flowId;
      if (!edgeKey) return;
      const next = cloneSchema(latestSchemaRef.current);
      const edge = (next.workflow.edges as MicroflowWorkflowEdgeJSON[]).find(item => {
        const flowId = (item.data as { flowId?: string } | undefined)?.flowId;
        return item.id === edgeKey || flowId === edgeKey;
      });
      if (!edge) return;
      const edgeData = (edge.data as FlowGramMicroflowEdgeData | undefined) ?? ({
        flowId: edge.id,
        flowKind: "sequence",
        edgeKind: "sequence",
        isErrorHandler: false,
        caseValues: [],
        validationState: "valid",
      } as FlowGramMicroflowEdgeData);
      const sourcePortBeforeCommit = edge.sourcePortID;
      const normalized = normalizeLineLabelByEdgeKind(edgeData.edgeKind, sourcePortBeforeCommit, detail.value ?? "");
      if (!normalized) {
        Toast.warning("当前连线标签不支持该值。");
        return;
      }
      const sourceNode = workflowNodeById(next.workflow, edge.sourceNodeID) as MicroflowWorkflowNodeJSON | undefined;
      const sourceKind = (sourceNode?.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind;
      const isDecisionLike = edgeData.edgeKind === "decisionCondition" || sourceKind === "exclusiveSplit" || sourceKind === "inheritanceSplit" || Array.isArray(edge.caseValues);
      if (isDecisionLike) {
        const canonicalDecision = normalizeDecisionLabel(normalized);
        const normalizedCase = canonicalDecision === "else" ? null : canonicalDecision === "true" ? true : canonicalDecision === "false" ? false : undefined;
        if (canonicalDecision) {
          edge.sourcePortID = canonicalDecision === "else" ? "decision:else" : `decision:${canonicalDecision}`;
          edge.caseValues = normalizedCase === null
            ? []
            : [{ kind: "boolean", value: normalizedCase }];
        }
        edge.data = {
          ...(edge.data as FlowGramMicroflowEdgeData | undefined),
          edgeKind: "decisionCondition",
          label: normalized,
          ...(canonicalDecision
            ? {
              caseValues: normalizedCase === null
                ? []
                : [{ kind: "boolean", value: normalizedCase }],
            }
            : {}),
        };
      } else {
        const sourcePortHint = sourcePortBeforeCommit ?? "";
        const canonical = canonicalBranchLabel(sourcePortHint, normalized);
        const hasSemanticPort = sourcePortHint.startsWith("approval:") || sourcePortHint.startsWith("loop:") || sourcePortHint.startsWith("error:");
        if (sourcePortHint.startsWith("approval:") && canonical) {
          edge.sourcePortID = `approval:${canonical}`;
        } else if (sourcePortHint.startsWith("loop:") && canonical) {
          edge.sourcePortID = `loop:${canonical}`;
        } else if (sourcePortHint.startsWith("error:") && canonical) {
          edge.sourcePortID = `error:${canonical}`;
        } else if (hasSemanticPort) {
          edge.sourcePortID = sourcePortHint;
        } else {
          edge.sourcePortID = normalized.includes(":") ? normalized : `${edgeData.edgeKind}:${normalized}`;
        }
        edge.data = { ...(edge.data as FlowGramMicroflowEdgeData | undefined), label: normalized };
      }
      syncBranchLabelToSourceNode(next, edge, normalized, sourcePortBeforeCommit);
      commitSchema(next, "property");
    };
    const onQuickFix = (event: Event) => {
      const detail = (event as CustomEvent<InlineQuickFixDetail>).detail;
      if (props.readonly) return;
      if (!detail?.nodeId) return;
      if (detail.actionKind === "createMissingFlow") {
        const branchCandidate = (detail as { branchValue?: unknown }).branchValue ?? detail.value;
        const branchValue = typeof branchCandidate === "boolean"
          ? branchCandidate
          : (branchCandidate === 1 ? true : branchCandidate === 0 ? false : undefined);
        const normalizedBranchValue = branchValue ?? (branchCandidate === "true" ? true : branchCandidate === "false" ? false : undefined);
        if (normalizedBranchValue === undefined) {
          Toast.warning(labels.quickFixUnavailable ?? "Quick fix is not available for this issue.");
          return;
        }
        const next = cloneSchema(latestSchemaRef.current);
        const exists = (next.workflow.edges as MicroflowWorkflowEdgeJSON[]).some(edge => {
          if (edge.sourceNodeID !== detail.nodeId) {
            return false;
          }
          const inlineCases = Array.isArray(edge.caseValues) ? (edge.caseValues as Array<{ kind?: string; value?: unknown }>) : [];
          const rawDataCases = (edge.data as { caseValues?: unknown } | undefined)?.caseValues;
          const dataCases = Array.isArray(rawDataCases) ? (rawDataCases as Array<{ kind?: string; value?: unknown }>) : [];
          const cases = [...inlineCases, ...dataCases];
          return cases.some(item => item?.kind === "boolean" && item?.value === normalizedBranchValue);
        });
        if (!exists) {
          const flowId = `flow-${detail.nodeId}-${normalizedBranchValue ? "true" : "false"}-${Date.now()}`;
          (next.workflow.edges as MicroflowWorkflowEdgeJSON[]).push({
            id: flowId,
            sourceNodeID: detail.nodeId,
            sourcePortID: `decision:${normalizedBranchValue ? "true" : "false"}`,
            targetNodeID: "end",
            targetPortID: "in",
            caseValues: [{ kind: "boolean", value: normalizedBranchValue }],
            data: {
              flowId,
              edgeKind: "decisionCondition",
              label: normalizedBranchValue ? "true" : "false",
              caseValues: [{ kind: "boolean", value: normalizedBranchValue }],
            } as unknown as Record<string, unknown>,
          });
          commitSchema(next, "property");
        }
        return;
      }
      if (detail.actionKind !== "setFieldValue" || !detail.fieldPath) return;
      applyInlineFieldCommit({ nodeId: detail.nodeId, fieldPath: detail.fieldPath, value: String(detail.value ?? "") });
    };
    window.addEventListener(MICROFLOW_INLINE_FIELD_COMMIT_EVENT, onFieldCommit as EventListener);
    window.addEventListener(MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT, onLineLabelCommit as EventListener);
    window.addEventListener(MICROFLOW_INLINE_QUICK_FIX_EVENT, onQuickFix as EventListener);
    return () => {
      window.removeEventListener(MICROFLOW_INLINE_FIELD_COMMIT_EVENT, onFieldCommit as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT, onLineLabelCommit as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_QUICK_FIX_EVENT, onQuickFix as EventListener);
    };
  }, [applyInlineFieldCommit, commitSchema, props.readonly]);

  const handleUndo = useCallback(() => {
    const previous = historyPast.at(-1);
    if (!previous) {
      return;
    }
    const nextPast = historyPast.slice(0, -1);
    const nextFuture = [cloneSchema(latestSchemaRef.current), ...historyFuture].slice(0, 100);
    const next = normalizeWorkflowCompatShape(cloneSchema(previous));
    latestSchemaRef.current = next;
    setSchema(next);
    setHistoryPast(nextPast);
    setHistoryFuture(nextFuture);
    setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
    props.onSchemaChange?.(next);
  }, [historyFuture, historyPast, props]);

  const handleRedo = useCallback(() => {
    const nextFuture = historyFuture[0];
    if (!nextFuture) {
      return;
    }
    const remainFuture = historyFuture.slice(1);
    const nextPast = [...historyPast, cloneSchema(latestSchemaRef.current)].slice(-100);
    const next = normalizeWorkflowCompatShape(cloneSchema(nextFuture));
    latestSchemaRef.current = next;
    setSchema(next);
    setHistoryPast(nextPast);
    setHistoryFuture(remainFuture);
    setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
    props.onSchemaChange?.(next);
  }, [historyFuture, historyPast, props]);

  const runValidation = useCallback(async (mode: MicroflowValidationMode = "edit") => {
    setValidationStatus("validating");
    try {
      const localIssues = validateNativeSchema(latestSchemaRef.current, mode);
      setIssues(localIssues);
      const result: ValidateMicroflowResponse = {
        valid: !localIssues.some(item => item.severity === "error"),
        issues: localIssues,
      };
      props.onValidateComplete?.(result);
      props.onValidationStateChange?.({
        microflowId: latestSchemaRef.current.id,
        issues: localIssues,
        status: result.valid ? "valid" : "invalid",
        lastValidatedAt: new Date(),
      });
      setValidationStatus(result.valid ? "valid" : "invalid");
      setLastValidatedAt(new Date());
      return { issues: localIssues, summary: summarizeIssues(localIssues) };
    } catch (error) {
      const validationIssue = issue(`${latestSchemaRef.current.id}:validation-error`, error instanceof Error ? error.message : String(error), "MF_VALIDATION_SERVICE_ERROR");
      setIssues([validationIssue]);
      setValidationStatus("invalid");
      return { issues: [validationIssue], summary: summarizeIssues([validationIssue]) };
    }
  }, [props]);

  const handleSave = useCallback(async (): Promise<boolean> => {
    if (props.readonly || saving) {
      return false;
    }
    setSaving(true);
    try {
      const validation = await runValidation("save");
      const blockers = validation.issues.filter(item => item.blockSave && item.severity === "error");
      if (blockers.length > 0 || validation.summary.errorCount > 0) {
        if (LEGACY_BOTTOM_PANEL_ENABLED) {
          setBottomDockMode("peek");
          setBottomTab("problems");
        }
        Toast.error(`保存被 ${blockers.length || validation.summary.errorCount} 个校验错误阻止。`);
        return false;
      }
      const response = await props.apiClient?.saveMicroflow({ schema: latestSchemaRef.current });
      savedSchemaSignatureRef.current = schemaWorkflowSignature(latestSchemaRef.current);
      setDirty(false);
      if (response) {
        props.onSaveComplete?.(response);
      }
      Toast.success("保存成功");
      return true;
    } finally {
      setSaving(false);
    }
  }, [props, runValidation, saving]);

  const handleTestRun = useCallback(async () => {
    if (saving || running || props.readonly || !latestSchemaRef.current.id) {
      return;
    }
    const saved = await handleSave();
    if (!saved) {
      return;
    }
    const validation = await runValidation("testRun");
    if (validation.summary.errorCount > 0) {
      if (LEGACY_BOTTOM_PANEL_ENABLED) {
        setBottomDockMode("peek");
        setBottomTab("problems");
      }
      Toast.warning("存在校验错误，无法运行。");
      return;
    }
    setRuntimeServiceError(undefined);
    setTestRunModalOpen(true);
  }, [handleSave, props.readonly, runValidation, running, saving]);

  const executeTestRun = useCallback(async (input: MicroflowTestRunInput) => {
    if (!props.apiClient) {
      Toast.warning("当前未配置运行适配器。");
      return;
    }
    setRunning(true);
    setRuntimeServiceError(undefined);
    try {
      if (dirty) {
        const saved = await handleSave();
        if (!saved) {
          return;
        }
      }
      const response = await props.apiClient.testRunMicroflow({
        microflowId: latestSchemaRef.current.id,
        input: input.parameters,
        schema: latestSchemaRef.current,
        options: input.options,
      });
      setLastRunSession(response.session);
      const trace = response.session.trace ?? [];
      const firstFailed = trace.find(frame => frame.status === "failed" && typeof frame.objectId === "string")?.objectId;
      if (firstFailed) {
        setNodeViewModes(current => ({
          ...Object.fromEntries(Object.entries(current).filter(([, mode]) => mode !== "inspectingError")),
          ...Object.fromEntries(trace.filter(frame => frame.status === "failed" && frame.objectId && frame.objectId !== firstFailed).map(frame => [String(frame.objectId), "expanded" as MicroflowNodeViewMode])),
          [firstFailed]: "inspectingError",
        }));
      } else {
        const firstVisited = trace.find(frame => typeof frame.objectId === "string")?.objectId;
        if (firstVisited) {
          setNodeViewModes(current => {
            const next: Record<string, MicroflowNodeViewMode> = {};
            for (const [nodeId, mode] of Object.entries(current)) {
              if (mode !== "inspectingError") {
                next[nodeId] = mode;
              }
            }
            next[firstVisited] = "inspectingRuntime";
            return next;
          });
        }
      }
      if (input.sampleId) {
        setTestRunSamples(current => current.map(sample => sample.id === input.sampleId
          ? {
            ...sample,
            previousResult: sample.lastResult,
            lastResult: response.session.output,
            lastStatus: response.session.status,
            lastRunId: response.session.id,
            lastRunAt: response.session.endedAt ?? response.session.startedAt,
            updatedAt: new Date().toISOString(),
          }
          : sample));
      }
      setBottomDockMode("collapsed");
      props.onTestRunComplete?.(response);
      Toast[response.status === "succeeded" ? "success" : "error"](`Run ${response.status}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setRuntimeServiceError(message);
      setBottomDockMode("collapsed");
      Toast.error(message);
    } finally {
      setRunning(false);
    }
  }, [dirty, handleSave, props]);

  const saveTestRunSample = useCallback((sample: Omit<MicroflowTestRunSample, "id" | "updatedAt"> & { id?: string }) => {
    const id = sample.id ?? `sample-${Date.now()}`;
    const now = new Date().toISOString();
    setTestRunSamples(current => [{
      id,
      name: sample.name,
      parameters: sample.parameters,
      expectedResult: sample.expectedResult,
      lastResult: sample.lastResult,
      lastStatus: sample.lastStatus,
      lastRunId: sample.lastRunId,
      lastRunAt: sample.lastRunAt,
      previousResult: sample.previousResult,
      updatedAt: now,
    }, ...current.filter(item => item.id !== id)].slice(0, 20));
  }, []);

  const runAllTestRunSamples = useCallback(async (samples: MicroflowTestRunSample[], options?: MicroflowTestRunInput["options"]) => {
    for (const sample of samples) {
      await executeTestRun({ parameters: sample.parameters, options, sampleId: sample.id });
    }
  }, [executeTestRun]);

  const handlePublish = useCallback(async () => {
    if (!props.onPublish) {
      Toast.warning("Publish handler is not configured.");
      return;
    }
    await props.onPublish(latestSchemaRef.current);
  }, [props]);

  const handleAddNode = useCallback((item: Parameters<NonNullable<React.ComponentProps<typeof MicroflowNodePanel>["onAddNode"]>>[0], options?: { position?: { x: number; y: number } }) => {
    const position = options?.position ?? { x: 360 + schema.workflow.nodes.length * 80, y: 240 };
    const node = createWorkflowNodeFromPanelItem(item, position, schema.workflow.nodes.map(existing => existing.id));
    const next = selectionPatch({
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [...schema.workflow.nodes, node] as MicroflowWorkflowJSON["nodes"],
      },
    }, { objectId: node.id, flowId: undefined, collectionId: "root-collection", objectIds: [node.id], flowIds: [], mode: "single" });
    commitSchema(next, "workflow");
  }, [commitSchema, schema]);

  const clearSelection = useCallback(() => {
    setContextMenu(undefined);
    commitSchema(selectionPatch(schema, { mode: "none", objectIds: [], flowIds: [] }), "selection", { pushHistory: false, dirty: false });
  }, [commitSchema, schema]);

  const handleDeleteSelection = useCallback((targetSelection?: FlowGramMicroflowSelection) => {
    if (props.readonly) {
      return;
    }
    const sourceSchema = latestSchemaRef.current;
    const selection = targetSelection ?? sourceSchema.editor.selection;
    const hasSelection = Boolean(selection.objectId || selection.flowId || selection.objectIds?.length || selection.flowIds?.length);
    if (!hasSelection) {
      return;
    }
    setContextMenu(undefined);
    commitSchema(deleteSelection(sourceSchema, selection), "delete");
  }, [commitSchema, props.readonly]);

  const handleAutoLayout = useCallback(() => {
    commitSchema(applyAutoLayout(schema), "layout");
    Toast.success("Auto layout applied.");
  }, [commitSchema, schema]);

  const handleEditorShortcut = useCallback((event: {
    key: string;
    target: EventTarget | null;
    ctrlKey: boolean;
    metaKey: boolean;
    shiftKey: boolean;
    defaultPrevented?: boolean;
    preventDefault: () => void;
  }) => {
    if (event.defaultPrevented || isEditableElement(event.target)) {
      return;
    }
    const key = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && key === "z" && !event.shiftKey) {
      event.preventDefault();
      handleUndo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && (key === "y" || (key === "z" && event.shiftKey))) {
      event.preventDefault();
      handleRedo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "s") {
      event.preventDefault();
      void handleSave();
      return;
    }
    if ((key === "delete" || key === "backspace") && !props.readonly) {
      event.preventDefault();
      handleDeleteSelection();
      return;
    }
    if (key === "escape") {
      event.preventDefault();
      clearSelection();
    }
  }, [clearSelection, handleDeleteSelection, handleRedo, handleSave, handleUndo, props.readonly]);

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    handleEditorShortcut(event);
  };

  useEffect(() => {
    const handleDocumentKeyDown = (event: globalThis.KeyboardEvent) => {
      const shell = shellRef.current;
      if (!shell) {
        return;
      }
      const targetElement = event.target instanceof HTMLElement ? event.target : null;
      const activeElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const activeInEditor = targetElement ? shell.contains(targetElement) : activeElement ? shell.contains(activeElement) : false;
      if (!activeInEditor) {
        return;
      }
      handleEditorShortcut(event);
    };
    document.addEventListener("keydown", handleDocumentKeyDown, true);
    return () => document.removeEventListener("keydown", handleDocumentKeyDown, true);
  }, [handleEditorShortcut]);

  const layoutState = useMemo<MicroflowWorkbenchLayoutState>(() => ({
    shellMode,
    activeBottomTab: bottomTab,
    bottomDockMode,
    focusMode: false,
    minimapVisible: schema.editor.showMiniMap === true,
    gridVisible: schema.editor.gridEnabled !== false,
  }), [bottomDockMode, bottomTab, schema.editor.gridEnabled, schema.editor.showMiniMap, shellMode]);

  const workbenchStatus = useMemo<MicroflowWorkbenchStatus>(() => ({
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    dirty,
    saving,
    running,
    validationStatus,
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    canUndo: historyPast.length > 0,
    canRedo: historyFuture.length > 0,
    zoomPercent: Math.round((schema.editor.viewport?.zoom ?? 1) * 100),
    hasRunSession: false,
    fullscreen: false,
    activeBottomTab: bottomTab,
    bottomDockMode,
    sessionHydrated: false,
    traceHydrated: false,
    debugSessionHydrated: false,
    degradedRunSession: false,
    layout: layoutState,
  }), [bottomDockMode, bottomTab, dirty, historyFuture.length, historyPast.length, issues, layoutState, running, saving, schema.editor.viewport?.zoom, schema.id, schema.schemaVersion, validationStatus]);

  useEffect(() => {
    props.onLayoutStateChange?.(layoutState);
  }, [layoutState, props]);

  useEffect(() => {
    props.onWorkbenchStatusChange?.(workbenchStatus);
  }, [props, workbenchStatus]);

  useEffect(() => {
    if (!contextMenu) {
      return undefined;
    }
    const close = (event: MouseEvent) => {
      const target = event.target instanceof Node ? event.target : null;
      if (target && contextMenuRef.current?.contains(target)) {
        return;
      }
      setContextMenu(undefined);
    };
    const closeOnScroll = () => setContextMenu(undefined);
    window.addEventListener("mousedown", close);
    window.addEventListener("scroll", closeOnScroll, true);
    return () => {
      window.removeEventListener("mousedown", close);
      window.removeEventListener("scroll", closeOnScroll, true);
    };
  }, [contextMenu]);

  useImperativeHandle(props.editorRef, () => ({
    save: async () => {
      await handleSave();
    },
    validate: async () => {
      await runValidation("save");
    },
    runTest: async () => {
      await handleTestRun();
    },
    runDebug: async () => {
      if (LEGACY_BOTTOM_PANEL_ENABLED) {
        setBottomDockMode("peek");
        setBottomTab("debug");
      }
      await handleTestRun();
    },
    publish: handlePublish,
    undo: handleUndo,
    redo: handleRedo,
    autoLayout: handleAutoLayout,
    fitView: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")),
    zoomIn: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: 0.1 } })),
    zoomOut: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: -0.1 } })),
    setZoom: (zoom: number) => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { zoom } })),
    toggleFullscreen: () => undefined,
    toggleFocusMode: () => undefined,
    toggleMinimap: () => {
      commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: !schema.editor.showMiniMap } }, "layout");
    },
    resetLayout: () => undefined,
    getStatus: () => workbenchStatus as MicroflowEditorStatusSnapshot,
    openBottomTab: (tab: MicroflowWorkbenchBottomTab) => {
      setBottomTab(tab);
      if (LEGACY_BOTTOM_PANEL_ENABLED) {
        setBottomDockMode("peek");
      }
    },
    setBottomDockMode,
    getLayoutState: () => layoutState,
    configureAllNodeAcceptance120: async () => {
      const next = buildAcceptance120SchemaFixture(latestSchemaRef.current);
      commitSchema(next, "workflow");
      setRightOpen(false);
      setLeftOpen(false);
      const saved = await handleSave();
      Toast[saved ? "success" : "warning"](saved ? "已配置并保存全节点验收计算图，期望输出 120。" : "已配置全节点验收计算图，但保存未完成，请查看 Problems。");
    },
  }), [commitSchema, handleAutoLayout, handlePublish, handleRedo, handleSave, handleTestRun, handleUndo, layoutState, runValidation, schema, workbenchStatus]);

  const shellStyle: CSSProperties = {
    display: "grid",
    gridTemplateRows: [
      toolbarMode === "internal" ? "60px" : undefined,
      "minmax(0, 1fr)",
      LEGACY_BOTTOM_PANEL_ENABLED && bottomOpen ? `${BOTTOM_DOCK_PEEK_HEIGHT_PX}px` : undefined,
      `${BOTTOM_STRIP_HEIGHT_PX}px`,
    ].filter(Boolean).join(" "),
    height: "100%",
    minHeight: 0,
    position: "relative",
    background: "var(--semi-color-bg-0, #f7f8fa)",
  };
  const bodyStyle: CSSProperties = {
    display: "grid",
    gridTemplateColumns: `${RAIL_WIDTH_PX}px minmax(0, 1fr) ${RAIL_WIDTH_PX}px`,
    minHeight: 0,
    minWidth: 0,
    overflow: "hidden",
    position: "relative",
  };
  const leftDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    bottom: 0,
    left: RAIL_WIDTH_PX,
    zIndex: 34,
    width: LEFT_PANEL_EXPANDED_PX,
    boxShadow: "8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const rightDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    right: RAIL_WIDTH_PX,
    bottom: 0,
    zIndex: 34,
    boxShadow: "-8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const selectedNode = workflowNodeById(schema.workflow, schema.editor.selection.objectId) as MicroflowWorkflowNodeJSON | undefined;
  const selectedFlow = workflowEdgeById(schema.workflow, schema.editor.selection.flowId) as MicroflowWorkflowEdgeJSON | undefined;

  return (
    <div ref={shellRef} data-testid="microflow-editor-shell" data-microflow-id={schema.id} style={shellStyle} tabIndex={0} onKeyDown={handleKeyDown}>
      {toolbarMode === "internal" ? (
        <div data-testid="microflow-editor-toolbar" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: "8px 12px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
          <Space style={{ minWidth: 0, overflow: "hidden" }}>
            {props.toolbarPrefix}
            <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
            <Tag>{schema.schemaVersion}</Tag>
            {dirty ? <Tag color="orange">dirty</Tag> : <Tag color="green">saved</Tag>}
            <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          </Space>
          <Space>
            <Tooltip content={labels.undo}><Button icon={<IconUndo />} disabled={historyPast.length === 0} onClick={handleUndo} /></Tooltip>
            <Tooltip content={labels.redo}><Button icon={<IconRedo />} disabled={historyFuture.length === 0} onClick={handleRedo} /></Tooltip>
            <Button data-testid="microflow-editor-validate" icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={() => void runValidation("save")}>{labels.validate}</Button>
            <Button data-testid="microflow-editor-run" icon={<IconPlay />} loading={running} disabled={saving || props.readonly} onClick={() => void handleTestRun()}>{labels.testRun}</Button>
            <Button data-testid="microflow-editor-save" icon={<IconSave />} type="primary" loading={saving} disabled={saving || props.readonly || !dirty} onClick={() => void handleSave()}>{labels.save}</Button>
            {props.toolbarSuffix}
          </Space>
        </div>
      ) : null}
      <div style={bodyStyle}>
        <button
          type="button"
          data-testid="microflow-node-panel-rail"
          aria-label={leftOpen ? "节点面板已展开" : "展开节点面板"}
          aria-expanded={leftOpen}
          style={{
            border: 0,
            borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
            background: leftOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
            color: leftOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
            cursor: "pointer",
          }}
          onClick={() => setLeftOpen(true)}
        >
          <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.nodePanel}</Text>
        </button>
        {leftOpen ? (
          <div data-testid="microflow-editor-left-panel" style={{ ...panelStyle(), ...leftDockStyle }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 8 }}>
              <Text strong>{labels.nodePanel}</Text>
              <Button aria-label="折叠节点面板" size="small" theme="borderless" icon={<IconClose />} onClick={() => setLeftOpen(false)} />
            </div>
            <MicroflowNodePanel
              favoriteNodeKeys={[]}
              onFavoriteChange={() => undefined}
              onAddNode={(item, options) => handleAddNode(item, { position: options?.position })}
              onShowDocumentation={item => {
                Toast.info(item.documentation.summary);
              }}
              labels={props.nodePanelLabels}
              createContext={{ microflowId: schema.id, moduleId: schema.moduleId, metadataAvailable: Boolean(props.metadataCatalog), schemaLoaded: true, readonly: props.readonly }}
            />
          </div>
        ) : null}
        <div data-testid="microflow-canvas" style={{ minWidth: 0, minHeight: 0, display: "contents" }}>
          <FlowGramMicroflowNativeCanvas
            schema={schema}
            validationIssues={issues}
            runtimeTrace={lastRunSession?.trace ?? []}
            nodeViewModes={nodeViewModes}
            focusObjectId={focusObjectId}
            focusRequestKey={focusRequestSeq}
            readonly={props.readonly}
            onSchemaChange={(nextSchema, reason) => {
              commitSchema(nextSchema, reason === "flowgramNodeMove" ? "workflow" : "workflow");
            }}
            onSelectionChange={selection => {
              if (isSameSelection(selection, latestSchemaRef.current.editor.selection)) {
                return;
              }
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
            }}
            onCanvasBlankClick={clearSelection}
            onNodeContextMenu={(selection, point) => {
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
              setContextMenu({ x: point.x, y: point.y, selection });
            }}
            onDropRegistryItem={(item, position) => handleAddNode(item, { position })}
            canUndo={historyPast.length > 0}
            canRedo={historyFuture.length > 0}
            onUndo={handleUndo}
            onRedo={handleRedo}
            onAutoLayout={handleAutoLayout}
            onViewportChange={(viewport, options) => {
              commitSchema({ ...schema, editor: { ...schema.editor, viewport, zoom: viewport.zoom } }, "selection", { pushHistory: false, dirty: options?.skipDirty === true ? false : true });
            }}
            onToggleMiniMap={visible => commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: visible } }, "layout")}
            onToggleGrid={enabled => commitSchema({ ...schema, editor: { ...schema.editor, gridEnabled: enabled } }, "layout")}
            dirty={dirty}
            saving={saving}
            validating={validationStatus === "validating"}
            onOpenProblemsPanel={() => {
              if (LEGACY_BOTTOM_PANEL_ENABLED) {
                setBottomDockMode("peek");
                setBottomTab("problems");
              }
            }}
          />
        </div>
        {LEGACY_PROPERTY_PANEL_ENABLED ? (
          <button
            type="button"
            data-testid="microflow-property-panel-rail"
            aria-label={rightOpen ? "属性面板已展开" : "展开属性面板"}
            aria-expanded={rightOpen}
            style={{
              border: 0,
              borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
              background: rightOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
              color: rightOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
              cursor: "pointer",
            }}
            onClick={() => setRightOpen(true)}
          >
            <IconSetting />
            <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.properties}</Text>
          </button>
        ) : null}
        {LEGACY_PROPERTY_PANEL_ENABLED && rightOpen ? (
          <div data-testid="microflow-property-panel" style={{ ...propertyPaneStyle(), ...rightDockStyle }}>
            <MicroflowPropertyPanel
              schemaProtocol="design"
              schema={schema}
              validationIssues={issues}
              traceFrames={[] as MicroflowTraceFrame[]}
              readonly={props.readonly}
              onSchemaChange={(next, reason) => commitSchema(next, reason === "deleteNode" || reason === "deleteFlow" ? "delete" : "property")}
              onLocateObject={objectId => {
                setFocusObjectId(objectId);
                setFocusRequestSeq(value => value + 1);
              }}
              onClose={() => setRightOpen(false)}
            />
          </div>
        ) : null}
        {contextMenu ? (
          <div
            ref={contextMenuRef}
            data-testid="microflow-canvas-context-menu"
            data-context-object-id={contextMenu.selection.objectId ?? ""}
            data-context-flow-id={contextMenu.selection.flowId ?? ""}
            data-context-object-ids={(contextMenu.selection.objectIds ?? []).join(",")}
            data-context-flow-ids={(contextMenu.selection.flowIds ?? []).join(",")}
            role="menu"
            style={{
              position: "fixed",
              ...contextMenuPosition(contextMenu),
              zIndex: 1000,
              minWidth: 168,
              padding: 6,
              display: "grid",
              gap: 4,
              border: "1px solid var(--semi-color-border, #e5e6eb)",
              borderRadius: 6,
              boxShadow: "0 8px 24px rgba(31, 35, 41, 0.14)",
              background: "var(--semi-color-bg-2, #fff)",
            }}
            onPointerDownCapture={event => {
              const target = event.target;
              if (!(target instanceof HTMLElement) || !target.closest("[data-menu-action='delete']")) {
                return;
              }
              event.preventDefault();
              event.stopPropagation();
              handleDeleteSelection(contextMenu.selection);
            }}
            onMouseDown={event => event.stopPropagation()}
            onClick={event => event.stopPropagation()}
          >
            {LEGACY_PROPERTY_PANEL_ENABLED ? (
              <Button
                role="menuitem"
                theme="borderless"
                style={{ justifyContent: "flex-start" }}
                onClick={() => {
                  commitSchema(selectionPatch(schema, contextMenu.selection), "selection", { pushHistory: false, dirty: false });
                  setRightOpen(true);
                  setContextMenu(undefined);
                }}
              >
                属性
              </Button>
            ) : null}
            {contextMenu.selection.objectId ? (
              <Button
                role="menuitem"
                theme="borderless"
                style={{ justifyContent: "flex-start" }}
                onClick={() => {
                  setFocusObjectId(contextMenu.selection.objectId);
                  setFocusRequestSeq(value => value + 1);
                  setContextMenu(undefined);
                }}
              >
                定位
              </Button>
            ) : null}
            <button
              type="button"
              role="menuitem"
              data-testid="microflow-canvas-context-menu-delete"
              data-menu-action="delete"
              disabled={props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection)}
              style={{
                minHeight: 32,
                display: "flex",
                alignItems: "center",
                gap: 8,
                justifyContent: "flex-start",
                border: 0,
                borderRadius: 4,
                padding: "6px 12px",
                color: "var(--semi-color-danger, #f93920)",
                background: "transparent",
                cursor: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? "not-allowed" : "pointer",
                opacity: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? 0.45 : 1,
                font: "inherit",
              }}
              onPointerDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onMouseDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onClick={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
            >
              <IconDelete />
              删除
            </button>
          </div>
        ) : null}
        <aside className="microflow-node-status-legend" aria-label="节点状态说明">
          <Text strong className="microflow-node-status-legend__title">节点状态说明</Text>
          <div className="microflow-node-status-legend__list">
            {NODE_STATUS_LEGEND.map(item => (
              <div key={item.key} className={["microflow-node-status-legend__item", item.tone ? `is-${item.tone}` : ""].join(" ")}>
                <span className="microflow-node-status-legend__label">{item.label}</span>
                <span className="microflow-node-status-legend__desc">{item.description}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
      {LEGACY_BOTTOM_PANEL_ENABLED && bottomOpen ? (
        <div
          data-testid="microflow-bottom-panel"
          style={{
            minHeight: 0,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-1, #fff)",
            overflow: "hidden",
            padding: "8px 12px 12px",
          }}
        >
          <Tabs type="line" activeKey={bottomTab} onChange={key => setBottomTab(key as MicroflowWorkbenchBottomTab)}>
            <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
              <Space vertical align="start" style={{ width: "100%", maxHeight: 200, overflow: "auto" }}>
                {issues.length === 0 ? <Empty title="No problems" /> : issues.map(item => (
                  <Button
                    key={item.id}
                    theme="borderless"
                    type={item.severity === "error" ? "danger" : "tertiary"}
                    onClick={() => {
                      if (item.objectId) {
                        setFocusObjectId(item.objectId);
                        setFocusRequestSeq(value => value + 1);
                      }
                    }}
                  >
                    {item.code}: {item.message}
                  </Button>
                ))}
              </Space>
            </Tabs.TabPane>
            <Tabs.TabPane tab={labels.debug} itemKey="debug">
              <Empty title="Debug trace is generated by the runtime adapter" description="当前原生设计态会在运行前单向编译为 runtime schema。" />
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : null}
      <div style={{ height: BOTTOM_STRIP_HEIGHT_PX, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 12px", borderTop: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
        <Space>
          <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          <Tag>{workflowNodeCount(schema.workflow)} nodes</Tag>
          <Tag>{workflowEdgeCount(schema.workflow)} edges</Tag>
          {lastValidatedAt ? <Text size="small" type="tertiary">{lastValidatedAt.toLocaleTimeString()}</Text> : null}
        </Space>
        <Space>
          {selectedNode ? <Tag color="blue">Node {selectedNode.id}</Tag> : null}
          {selectedFlow ? <Tag color="blue">Flow {(selectedFlow.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? selectedFlow.id}</Tag> : null}
          <Button size="small" theme="borderless" icon={<IconDelete />} disabled={props.readonly || !(selectedNode || selectedFlow)} onClick={() => handleDeleteSelection()} />
          {LEGACY_BOTTOM_PANEL_ENABLED ? (
            <Button size="small" theme="borderless" onClick={() => setBottomDockMode(mode => mode === "collapsed" ? "peek" : "collapsed")}>{bottomOpen ? "Hide" : "Problems"}</Button>
          ) : null}
        </Space>
      </div>
      <MicroflowTestRunModal
        visible={testRunModalOpen}
        schema={schema}
        running={running}
        dirty={dirty}
        validationErrorCount={issues.filter(item => item.severity === "error").length}
        values={testRunValues}
        lastSession={lastRunSession}
        serviceError={runtimeServiceError}
        samples={testRunSamples}
        onCancel={() => setTestRunModalOpen(false)}
        onValuesChange={setTestRunValues}
        onSaveSample={saveTestRunSample}
        onRunAllSamples={runAllTestRunSamples}
        onRun={executeTestRun}
      />
    </div>
  );
}
