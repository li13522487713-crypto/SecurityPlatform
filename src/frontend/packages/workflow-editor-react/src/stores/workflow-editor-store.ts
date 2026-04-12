import { create } from "zustand";
import type { TraceStepItem } from "../components/TracePanel";
import type { NodeTemplateMetadata, NodeTypeMetadata } from "../types";
import type { CanvasConnection, CanvasNode, EdgeRuntimeState, WorkflowViewportState } from "../editor/workflow-editor-state";
import { INITIAL_CONNECTIONS, INITIAL_NODES } from "../editor/workflow-editor-state";

export interface NodeExecutionState {
  state: "idle" | "running" | "success" | "failed" | "skipped" | "blocked";
  hint?: string;
}

export interface WorkflowEditorState {
  workflowName: string;
  isDirty: boolean;
  lastSavedAt: number | null;
  zoom: number;
  pan: { x: number; y: number };
  selectedNodeKeys: string[];
  canvasNodes: CanvasNode[];
  canvasConnections: CanvasConnection[];
  canvasGlobals: Record<string, unknown>;
  nodeTypesMeta: NodeTypeMetadata[];
  nodeTemplates: NodeTemplateMetadata[];
  logs: string[];
  traceSteps: TraceStepItem[];
  executionStateByNodeKey: Record<string, NodeExecutionState>;
  edgeStateByConnectionKey: Record<string, EdgeRuntimeState>;
  saving: boolean;
  testRunning: boolean;
  debugRunning: boolean;
  testInputJson: string;
  testRunMode: "stream" | "sync";
  testRunSource: "published" | "draft";
  debugNodeKey: string;
  debugInputJson: string;
  debugOutput: string;
  saveVersion: number;
  setWorkflowName: (name: string) => void;
  setDirty: (dirty: boolean) => void;
  setLastSavedAt: (savedAt: number | null) => void;
  setZoom: (zoom: number) => void;
  setPan: (pan: { x: number; y: number }) => void;
  setSelectedNodeKeys: (keys: string[]) => void;
  setCanvasNodes: (nodes: CanvasNode[]) => void;
  setCanvasConnections: (connections: CanvasConnection[]) => void;
  setCanvasGlobals: (globals: Record<string, unknown>) => void;
  setNodeTypesMeta: (meta: NodeTypeMetadata[]) => void;
  setNodeTemplates: (templates: NodeTemplateMetadata[]) => void;
  setSaving: (saving: boolean) => void;
  setTestRunning: (running: boolean) => void;
  setDebugRunning: (running: boolean) => void;
  setTestInputJson: (value: string) => void;
  setTestRunMode: (mode: "stream" | "sync") => void;
  setTestRunSource: (source: "published" | "draft") => void;
  setDebugNodeKey: (key: string) => void;
  setDebugInputJson: (value: string) => void;
  setDebugOutput: (value: string) => void;
  setSaveVersion: (version: number) => void;
  addSaveVersion: () => void;
  appendLog: (line: string) => void;
  appendTrace: (step: TraceStepItem) => void;
  clearTrace: () => void;
  setExecutionState: (nodeKey: string, next: NodeExecutionState) => void;
  setEdgeState: (lineKey: string, state: EdgeRuntimeState) => void;
  clearRuntimeState: () => void;
  setCanvasSnapshot: (payload: {
    nodes: CanvasNode[];
    connections: CanvasConnection[];
    globals: Record<string, unknown>;
    viewport?: WorkflowViewportState;
    workflowName?: string;
    isDirty?: boolean;
  }) => void;
  resetEditorState: () => void;
}

const DEFAULT_ZOOM = 100;

function sameStringArray(left: string[], right: string[]): boolean {
  if (left === right) {
    return true;
  }
  if (left.length !== right.length) {
    return false;
  }
  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) {
      return false;
    }
  }
  return true;
}

export const useWorkflowEditorStore = create<WorkflowEditorState>((set) => ({
  workflowName: "",
  isDirty: false,
  lastSavedAt: null,
  zoom: DEFAULT_ZOOM,
  pan: { x: 0, y: 0 },
  selectedNodeKeys: [],
  canvasNodes: INITIAL_NODES,
  canvasConnections: INITIAL_CONNECTIONS,
  canvasGlobals: {},
  nodeTypesMeta: [],
  nodeTemplates: [],
  logs: [],
  traceSteps: [],
  executionStateByNodeKey: {},
  edgeStateByConnectionKey: {},
  saving: false,
  testRunning: false,
  debugRunning: false,
  testInputJson: "{\"input\":\"hello\"}",
  testRunMode: "stream",
  testRunSource: "published",
  debugNodeKey: "",
  debugInputJson: "{\"input\":\"hello\"}",
  debugOutput: "",
  saveVersion: 0,
  setWorkflowName: (workflowName) => set({ workflowName }),
  setDirty: (isDirty) => set({ isDirty }),
  setLastSavedAt: (lastSavedAt) => set({ lastSavedAt }),
  setZoom: (zoom) => set({ zoom }),
  setPan: (pan) => set({ pan }),
  setSelectedNodeKeys: (selectedNodeKeys) =>
    set((state) => (sameStringArray(state.selectedNodeKeys, selectedNodeKeys) ? state : { selectedNodeKeys })),
  setCanvasNodes: (canvasNodes) => set({ canvasNodes }),
  setCanvasConnections: (canvasConnections) => set({ canvasConnections }),
  setCanvasGlobals: (canvasGlobals) => set({ canvasGlobals }),
  setNodeTypesMeta: (nodeTypesMeta) => set({ nodeTypesMeta }),
  setNodeTemplates: (nodeTemplates) => set({ nodeTemplates }),
  setSaving: (saving) => set({ saving }),
  setTestRunning: (testRunning) => set({ testRunning }),
  setDebugRunning: (debugRunning) => set({ debugRunning }),
  setTestInputJson: (testInputJson) => set({ testInputJson }),
  setTestRunMode: (testRunMode) => set({ testRunMode }),
  setTestRunSource: (testRunSource) => set({ testRunSource }),
  setDebugNodeKey: (debugNodeKey) => set({ debugNodeKey }),
  setDebugInputJson: (debugInputJson) => set({ debugInputJson }),
  setDebugOutput: (debugOutput) => set({ debugOutput }),
  setSaveVersion: (saveVersion) => set({ saveVersion }),
  addSaveVersion: () => set((state) => ({ saveVersion: state.saveVersion + 1 })),
  appendLog: (line) =>
    set((state) => ({
      logs: [...state.logs, `${new Date().toLocaleTimeString()} ${line}`]
    })),
  appendTrace: (step) =>
    set((state) => ({
      traceSteps: [...state.traceSteps, step]
    })),
  clearTrace: () => set({ traceSteps: [] }),
  setExecutionState: (nodeKey, next) =>
    set((state) => ({
      executionStateByNodeKey: {
        ...state.executionStateByNodeKey,
        [nodeKey]: next
      }
    })),
  setEdgeState: (lineKey, stateValue) =>
    set((state) => ({
      edgeStateByConnectionKey: {
        ...state.edgeStateByConnectionKey,
        [lineKey]: stateValue
      }
    })),
  clearRuntimeState: () =>
    set({
      executionStateByNodeKey: {},
      edgeStateByConnectionKey: {},
      traceSteps: []
    }),
  setCanvasSnapshot: (payload) =>
    set({
      canvasNodes: payload.nodes,
      canvasConnections: payload.connections,
      canvasGlobals: payload.globals,
      pan: payload.viewport ? { x: payload.viewport.x, y: payload.viewport.y } : { x: 0, y: 0 },
      zoom: payload.viewport ? payload.viewport.zoom : DEFAULT_ZOOM,
      workflowName: payload.workflowName ?? "",
      isDirty: payload.isDirty ?? false,
      lastSavedAt: payload.isDirty ? null : Date.now()
    }),
  resetEditorState: () =>
    set({
      workflowName: "",
      isDirty: false,
      lastSavedAt: null,
      zoom: DEFAULT_ZOOM,
      pan: { x: 0, y: 0 },
      selectedNodeKeys: [],
      canvasNodes: INITIAL_NODES,
      canvasConnections: INITIAL_CONNECTIONS,
      canvasGlobals: {},
      nodeTypesMeta: [],
      nodeTemplates: [],
      logs: [],
      traceSteps: [],
      executionStateByNodeKey: {},
      edgeStateByConnectionKey: {},
      saving: false,
      testRunning: false,
      debugRunning: false,
      testInputJson: "{\"input\":\"hello\"}",
      testRunMode: "stream",
      testRunSource: "published",
      debugNodeKey: "",
      debugInputJson: "{\"input\":\"hello\"}",
      debugOutput: "",
      saveVersion: 0
    })
}));
