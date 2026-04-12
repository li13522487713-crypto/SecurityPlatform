import { injectable } from "inversify";
import { NodeRegistry, createMetadataBundle, mergeNodeDefaults } from "../node-registry";
import type { WorkflowNodeRegistryV2 } from "../node-registry";
import type { CanvasConnection, CanvasNode } from "../editor/workflow-editor-state";
import { NODE_HEIGHT, NODE_WIDTH } from "../editor/workflow-editor-state";
import { useNodeSideSheetStore } from "../stores/node-side-sheet-store";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";
import { getAntiOverlapPosition } from "./utils/anti-overlap";
import { WorkflowDragService } from "./workflow-drag-service";

@injectable()
export class WorkflowEditService {
  private readonly registry = new NodeRegistry();

  constructor(private readonly dragService: WorkflowDragService) {}

  private buildUniqueNodeKey(baseType: string, existingNodes: CanvasNode[]): string {
    const normalizedBase = `${baseType.toLowerCase()}_${Date.now().toString(36)}`;
    if (!existingNodes.some((node) => node.key === normalizedBase)) {
      return normalizedBase;
    }
    let cursor = 1;
    let candidate = `${normalizedBase}_${cursor}`;
    while (existingNodes.some((node) => node.key === candidate)) {
      cursor += 1;
      candidate = `${normalizedBase}_${cursor}`;
    }
    return candidate;
  }

  addNode(type: string, nodeJson?: Partial<CanvasNode>, position?: { x: number; y: number }, isDrag?: boolean): CanvasNode | null {
    const state = useWorkflowEditorStore.getState();
    const definition = this.registry.resolve(type);
    const metadata = createMetadataBundle(state.nodeTypesMeta, state.nodeTemplates);
    const template = metadata.templatesMap.get(definition.type);
    const key = this.buildUniqueNodeKey(type, state.canvasNodes);
    const basePosition = position ?? { x: 120, y: 120 };
    const antiOverlap = getAntiOverlapPosition(state.canvasNodes, {
      position: basePosition,
      size: { width: NODE_WIDTH, height: NODE_HEIGHT }
    });
    const normalized = {
      key,
      type: definition.type,
      title: nodeJson?.title ?? definition.type,
      x: Math.round(antiOverlap.x),
      y: Math.round(antiOverlap.y),
      configs: mergeNodeDefaults(definition, template, nodeJson?.configs ?? {}),
      inputMappings: nodeJson?.inputMappings ?? {}
    } as CanvasNode;

    const nextNode = {
      ...normalized,
      ...nodeJson
    } as CanvasNode;

    const registryV2 = definition as WorkflowNodeRegistryV2;
    if (registryV2.onInit) {
      void registryV2.onInit(nextNode as unknown as Record<string, unknown>, { mode: isDrag ? "drag" : "click" });
    }
    state.setCanvasNodes([...state.canvasNodes, nextNode]);
    state.setSelectedNodeKeys([nextNode.key]);
    state.setDirty(true);

    if (isDrag) {
      this.dragService.endDrag();
    }
    this.focusNode(nextNode.key);
    return nextNode;
  }

  copyNode(nodeKey: string): CanvasNode | null {
    const state = useWorkflowEditorStore.getState();
    const node = state.canvasNodes.find((item) => item.key === nodeKey);
    if (!node) {
      return null;
    }
    const duplicate = structuredClone(node);
    duplicate.key = this.buildUniqueNodeKey(node.type, state.canvasNodes);
    duplicate.title = `${node.title}-副本`;
    duplicate.x += 48;
    duplicate.y += 48;
    return this.addNode(node.type, duplicate, { x: duplicate.x, y: duplicate.y });
  }

  recreateNodeJSON(node: CanvasNode): CanvasNode {
    const state = useWorkflowEditorStore.getState();
    return {
      ...structuredClone(node),
      key: this.buildUniqueNodeKey(node.type, state.canvasNodes)
    };
  }

  deleteNode(nodeKey: string): void {
    const state = useWorkflowEditorStore.getState();
    const currentNode = state.canvasNodes.find((node) => node.key === nodeKey);
    if (currentNode) {
      const definition = this.registry.resolve(currentNode.type) as WorkflowNodeRegistryV2;
      definition.onDispose?.(currentNode as unknown as Record<string, unknown>, { source: "deleteNode" });
    }
    const selectedSet = new Set([nodeKey]);
    const nextNodes = state.canvasNodes.filter((node) => !selectedSet.has(node.key));
    const nextConnections = state.canvasConnections.filter(
      (line: CanvasConnection) => !selectedSet.has(line.fromNode) && !selectedSet.has(line.toNode)
    );
    state.setCanvasNodes(nextNodes);
    state.setCanvasConnections(nextConnections);
    state.setSelectedNodeKeys(nextNodes.length > 0 ? [nextNodes[0].key] : []);
    state.setDirty(true);
    if (useNodeSideSheetStore.getState().currentNodeKey === nodeKey) {
      useNodeSideSheetStore.getState().closeSideSheet();
    }
  }

  focusNode(nodeKey: string): void {
    if (!nodeKey) {
      return;
    }
    const state = useWorkflowEditorStore.getState();
    state.setSelectedNodeKeys([nodeKey]);
    useNodeSideSheetStore.getState().openSideSheet(nodeKey);
  }
}
