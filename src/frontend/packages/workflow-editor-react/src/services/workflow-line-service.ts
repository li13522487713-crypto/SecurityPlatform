import { injectable } from "inversify";
import { validateConnectionCandidate } from "../editor/connection-rules";
import type { CanvasConnection } from "../editor/workflow-editor-state";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";
import { WorkflowDragService } from "./workflow-drag-service";

interface PortInfo {
  nodeKey: string;
  portKey: string;
}

@injectable()
export class WorkflowLineService {
  constructor(private readonly dragService: WorkflowDragService) {}

  getAllLines(): CanvasConnection[] {
    return useWorkflowEditorStore.getState().canvasConnections;
  }

  createLine(from: PortInfo, to: PortInfo): CanvasConnection | null {
    const state = useWorkflowEditorStore.getState();
    const candidate = {
      fromNode: from.nodeKey,
      fromPort: from.portKey,
      toNode: to.nodeKey,
      toPort: to.portKey
    };
    const fromPorts = [{ key: from.portKey, name: from.portKey, direction: "output" as const, dataType: "any", isRequired: false, maxConnections: 99 }];
    const toPorts = [{ key: to.portKey, name: to.portKey, direction: "input" as const, dataType: "any", isRequired: false, maxConnections: 99 }];
    const result = validateConnectionCandidate(candidate, state.canvasConnections, fromPorts, toPorts);
    if (!result.ok) {
      return null;
    }
    const line: CanvasConnection = {
      id: `conn_${from.nodeKey}_${from.portKey}_${to.nodeKey}_${to.portKey}_${Date.now().toString(36)}`,
      ...candidate,
      condition: null
    };
    state.setCanvasConnections([...state.canvasConnections, line]);
    state.setDirty(true);
    return line;
  }

  isError(fromId: string, toId?: string): boolean {
    if (!fromId || !toId) {
      return true;
    }
    return !useWorkflowEditorStore
      .getState()
      .canvasConnections.some((line) => line.fromNode === fromId || line.toNode === toId);
  }

  replaceLineByPort(oldPortInfo: PortInfo, newPortInfo: PortInfo): void {
    const state = useWorkflowEditorStore.getState();
    const next = state.canvasConnections.map((line) => {
      if (line.fromNode === oldPortInfo.nodeKey && line.fromPort === oldPortInfo.portKey) {
        return { ...line, fromNode: newPortInfo.nodeKey, fromPort: newPortInfo.portKey };
      }
      if (line.toNode === oldPortInfo.nodeKey && line.toPort === oldPortInfo.portKey) {
        return { ...line, toNode: newPortInfo.nodeKey, toPort: newPortInfo.portKey };
      }
      return line;
    });
    state.setCanvasConnections(next);
    state.setDirty(true);
  }

  onDragLineEnd(params: { fromPort?: PortInfo; toPort?: PortInfo }): { allowInsert: boolean; message?: string } {
    if (!params.fromPort || params.toPort) {
      return { allowInsert: false };
    }
    const canDrop = this.dragService.canDropToNode("TextProcessor");
    if (!canDrop.allowDrop) {
      return { allowInsert: false, message: canDrop.message };
    }
    return { allowInsert: true };
  }
}
