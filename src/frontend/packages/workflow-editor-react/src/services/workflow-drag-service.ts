import { injectable } from "inversify";
import type { CanvasNode } from "../editor/workflow-editor-state";
import { getCollisionTransform } from "./utils/collision";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";

export interface WorkflowDragState {
  isDragging: boolean;
  dragNode?: { type: string; json?: Record<string, unknown> };
  dropNode?: CanvasNode;
}

export interface DropResult {
  allowDrop: boolean;
  message?: string;
  dropNode?: CanvasNode;
}

@injectable()
export class WorkflowDragService {
  readonly state: WorkflowDragState = {
    isDragging: false
  };

  startDrag(dragNode: { type: string; json?: Record<string, unknown> }): void {
    this.state.isDragging = true;
    this.state.dragNode = dragNode;
  }

  endDrag(): void {
    this.state.isDragging = false;
    this.state.dragNode = undefined;
    this.state.dropNode = undefined;
  }

  computeCanDrop(coord: { x: number; y: number }): DropResult {
    const state = useWorkflowEditorStore.getState();
    const collision = getCollisionTransform(coord, state.canvasNodes, { width: 360, height: 160 });
    return this.canDropToNode(this.state.dragNode?.type, collision?.node);
  }

  canDrop(params: { coord: { x: number; y: number }; dragNode: { type: string; json?: Record<string, unknown> } }): boolean {
    this.state.dragNode = params.dragNode;
    const result = this.computeCanDrop(params.coord);
    this.state.dropNode = result.dropNode;
    return result.allowDrop;
  }

  canDropToNode(dragNodeType?: string, dropNode?: CanvasNode): DropResult {
    if (!dragNodeType) {
      return { allowDrop: false };
    }

    const normalizedDrop = dropNode;
    const dropType = normalizedDrop?.type;
    const isContainer = dropType === "Loop" || dropType === "Batch";

    if ((dragNodeType === "Entry" || dragNodeType === "Exit") && isContainer) {
      return { allowDrop: false, message: "开始/结束节点不能拖入容器。", dropNode: normalizedDrop };
    }

    if ((dragNodeType === "Loop" || dragNodeType === "Batch") && isContainer) {
      return { allowDrop: false, message: "循环/批处理节点不允许继续嵌套容器。", dropNode: normalizedDrop };
    }

    if (dragNodeType === "Break" || dragNodeType === "Continue" || dragNodeType === "VariableAssignerWithinLoop") {
      if (dropType !== "Loop") {
        return {
          allowDrop: false,
          message: "Break/Continue/循环赋值节点只能放在 Loop 子画布内。",
          dropNode: normalizedDrop
        };
      }
    }

    return {
      allowDrop: true,
      dropNode: normalizedDrop
    };
  }
}
