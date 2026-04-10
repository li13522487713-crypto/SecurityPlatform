import { defineStore } from "pinia";
import { ref } from "vue";
import type { CanvasSchema, NodeExecutionState } from "@/types/workflow-v2";

export interface WorkflowEditorHistoryItem {
  id: string;
  canvas: CanvasSchema;
  createdAt: string;
}

export const useWorkflowEditorStore = defineStore("workflow-editor", () => {
  const workflowId = ref<string>("");
  const canvas = ref<CanvasSchema>({ nodes: [], connections: [] });
  const selectedNodeKey = ref<string>("");
  const executionId = ref<string>("");
  const nodeStates = ref<Record<string, NodeExecutionState>>({});
  const variables = ref<Record<string, unknown>>({});
  const undoStack = ref<WorkflowEditorHistoryItem[]>([]);
  const redoStack = ref<WorkflowEditorHistoryItem[]>([]);

  function setWorkflow(id: string) {
    workflowId.value = id;
  }

  function setCanvas(nextCanvas: CanvasSchema) {
    canvas.value = nextCanvas;
  }

  function patchNodeState(nodeKey: string, state: NodeExecutionState) {
    nodeStates.value[nodeKey] = state;
  }

  function clearExecutionState() {
    executionId.value = "";
    nodeStates.value = {};
  }

  function snapshotHistory() {
    undoStack.value.push({
      id: crypto.randomUUID(),
      canvas: structuredClone(canvas.value),
      createdAt: new Date().toISOString()
    });
    if (undoStack.value.length > 100) {
      undoStack.value.shift();
    }
    redoStack.value = [];
  }

  function undo() {
    const latest = undoStack.value.pop();
    if (!latest) return;
    redoStack.value.push({
      id: crypto.randomUUID(),
      canvas: structuredClone(canvas.value),
      createdAt: new Date().toISOString()
    });
    canvas.value = structuredClone(latest.canvas);
  }

  function redo() {
    const latest = redoStack.value.pop();
    if (!latest) return;
    undoStack.value.push({
      id: crypto.randomUUID(),
      canvas: structuredClone(canvas.value),
      createdAt: new Date().toISOString()
    });
    canvas.value = structuredClone(latest.canvas);
  }

  return {
    workflowId,
    canvas,
    selectedNodeKey,
    executionId,
    nodeStates,
    variables,
    undoStack,
    redoStack,
    setWorkflow,
    setCanvas,
    patchNodeState,
    clearExecutionState,
    snapshotHistory,
    undo,
    redo
  };
});
