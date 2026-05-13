import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type DragEvent, type MouseEvent, type PointerEvent } from "react";

import { Button, Space, Toast } from "@douyinfe/semi-ui";
import { IconDelete } from "@douyinfe/semi-icons";
import {
  type FlowNodeEntity,
  PlaygroundReactRenderer,
  WorkflowDocument,
  WorkflowDragService,
  type WorkflowEdgeJSON,
  type WorkflowJSON,
  type WorkflowLineEntity,
  WorkflowLinesManager,
  WorkflowSelectService,
  SelectorBoxConfigEntity,
  useEntity,
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { DebugLoopIteration } from "../stores/debug-store";
import type { MicroflowRuntimeOverlayState } from "../runtime/runtime-overlay";
import {
  canCreateRegistryItem,
  canDragRegistryItem,
  getDisabledDragReason,
  hasMicroflowNodeDragType,
  microflowNodeRegistryByKey,
  objectKindFromRegistryItem,
  readMicroflowNodeDragPayload,
  takeMicroflowNodePointerDrag,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
import { toEditorGraph } from "../adapters/microflow-adapters";
import type {
  MicroflowDesignSchema,
  MicroflowPoint,
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import {
  MICROFLOW_ROOT_COLLECTION_ID,
  flattenFlowGramWorkflowForPersistence,
  nestLoopChildrenForFlowGram,
  workflowEdgeById,
  workflowNodeById,
} from "./flowgram-native-schema";
import { FlowGramMicroflowProvider } from "./FlowGramMicroflowProvider";
import {
  FlowGramMicroflowToolbar,
  microflowZoomViewportAtCanvasCenter,
  microflowZoomViewportAtLocalPoint,
} from "./FlowGramMicroflowToolbar";
import {
  isPointerTargetPanExempt,
  resolveViewportPanEndIntent,
  shouldViewportPanFromPointerDown,
  zoomViewportForPanToolWheel,
} from "./flowgram-canvas-interactions";
import { decorateWorkflow } from "./flowgram-workflow-decorate";
import {
  canDropRegistryObjectKindInLoop,
  findLoopParentAtPoint,
  isMicroflowDesignEdgeBusinessValid,
  normalizeMicroflowDesignEdges,
} from "./flowgram-design-edge-semantics";
import { getMendixMicroflowDropOffset, getMendixMicroflowNodeSize } from "./flowgram-node-geometry";
import { flowGramPortsForObjectKind } from "./adapters/flowgram-port-factory";
import { MICROFLOW_GRID_SIZE, clientPointToFlowGramPoint, logicalToContainer, snapMicroflowPoint } from "./adapters/flowgram-coordinate";
import { stripTransientWorkflowState } from "./transient-workflow-state";
import { MicroflowEdgeDataContext, MicroflowSelectedFlowIdContext } from "./FlowGramMicroflowTypes";
import { forceOrthogonalLineKind } from "./FlowGramMicroflowTypes";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData, FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";
import type { MicroflowNodeViewMode } from "./FlowGramMicroflowTypes";
import { MicroflowNodeUsageHighlightsContext, MicroflowNodeViewModesContext } from "./FlowGramMicroflowTypes";
import { FlowGramNodeToolbar } from "./FlowGramNodeToolbar";
import { MicroflowNodeSpotlight } from "./MicroflowNodeSpotlight";
import { findNearestInsertableEdgeFlowId } from "./drop-insert-utils";
import type { MicroflowNodeUsageHighlights } from "../variables";
import { summarizeMicroflowComplexity } from "../utils/microflow-validator";
import "@flowgram-adapter/free-layout-editor/css-load";
import "./styles/flowgram-microflow-canvas.css";
import "./styles/flowgram-microflow-port.css";
import "./styles/flowgram-microflow-line.css";

const INITIAL_START_VIEWPORT_ZOOM = 0.75;
const INITIAL_START_VIEWPORT_LEFT_PADDING = 120;
const INITIAL_START_VIEWPORT_TOP_RATIO = 0.32;

type FlowGramPlaygroundViewportConfig = {
  zoom?: number | ((zoom: number) => void);
  updateConfig?: (config: { zoom?: number; scrollX?: number; scrollY?: number }) => void;
};

type ReconnectFlowDragEndpoint = "source" | "target";
type ReconnectFlowInput = {
  flowId: string;
  dragEndpoint: ReconnectFlowDragEndpoint;
  targetObjectId: string;
  targetPortId: string;
};
type ReconnectFlowEvaluationResult = {
  allowed: boolean;
  message?: string;
  edgeKind?: string;
};
type ReconnectFlowCommitResult = {
  ok: boolean;
  message?: string;
};

export interface FlowGramMicroflowNativeCanvasProps {
  schema: MicroflowDesignSchema;
  validationIssues: MicroflowValidationIssue[];
  runtimeTrace: MicroflowTraceFrame[];
  runtimeOverlay?: MicroflowRuntimeOverlayState;
  loopIteration?: DebugLoopIteration;
  pausedNodeIds?: string[];
  breakpointNodeIds?: string[];
  conditionalBreakpointNodeIds?: string[];
  nodeViewModes?: Record<string, MicroflowNodeViewMode>;
  usageHighlights?: MicroflowNodeUsageHighlights;
  focusObjectId?: string;
  focusRequestKey?: number;
  readonly?: boolean;
  onSchemaChange: (nextSchema: MicroflowDesignSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onNodeClickChange?: (selection: FlowGramMicroflowSelection) => void;
  onNodeDoubleClick?: (selection: FlowGramMicroflowSelection) => void;
  onCanvasBlankClick?: (point?: { x: number; y: number }) => void;
  onCanvasBlankDoubleClick?: (point: { x: number; y: number }) => void;
  onNodeContextMenu?: (selection: FlowGramMicroflowSelection, point: { x: number; y: number }) => void;
  onNodeToolbarQuickAdd?: (objectId: string, point: { x: number; y: number }) => void;
  onNodeToolbarQuickConnect?: (objectId: string, item: import("../node-registry").MicroflowNodeRegistryItem) => void;
  onDropRegistryItem?: (
    item: MicroflowNodeRegistryItem,
    position: MicroflowPoint,
    payload: MicroflowNodeDragPayload,
    options?: { parentLoopObjectId?: string; insertFlowId?: string },
  ) => void;
  onEvaluateFlowReconnect?: (input: ReconnectFlowInput) => ReconnectFlowEvaluationResult;
  onReconnectFlow?: (input: ReconnectFlowInput) => ReconnectFlowCommitResult;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
  onViewportChange?: (viewport: MicroflowDesignSchema["editor"]["viewport"], options?: { skipDirty?: boolean }) => void;
  onToggleMiniMap?: (visible: boolean) => void;
  onToggleGrid?: (enabled: boolean) => void;
  dirty?: boolean;
  saving?: boolean;
  validating?: boolean;
  showBuiltInToolbar?: boolean;
  onOpenProblemsPanel?: () => void;
  /** Controlled pan tool: when `canvasPanToolActive` is set, it wins over internal state. */
  canvasPanToolActive?: boolean;
  onCanvasPanToolChange?: (active: boolean) => void;
  onDeleteSelection?: () => void;
  onDuplicateSelection?: () => void;
  onClearSelection?: () => void;
  onCanvasBlankContextMenu?: (point: { x: number; y: number }) => void;
  onRun?: () => void;
  onStopRun?: () => void;
  isRunning?: boolean;
  onNavigateToIssue?: (objectId: string) => void;
}

interface FlowGramMicroflowNativeCanvasInnerProps extends FlowGramMicroflowNativeCanvasProps {
  /** 由外层组件传入的持久化 ref，跨 structureKey remount 保持 viewport 初始化状态 */
  viewportInitializedRef?: React.MutableRefObject<boolean>;
}

type DisposableLineSnapshot = {
  id?: string | number;
  data?: { flowId?: string };
  sourceNodeID?: string | number;
  targetNodeID?: string | number;
  sourcePortID?: string | number;
  targetPortID?: string | number;
};

type DisposableLineCandidate = WorkflowLineEntity & {
  id?: string | number;
  info?: {
    from?: string | number;
    to?: string | number;
    fromPort?: string | number;
    toPort?: string | number;
  };
  data?: { flowId?: string };
};

function cloneWorkflow(workflow: WorkflowJSON | MicroflowWorkflowJSON): WorkflowJSON {
  return JSON.parse(JSON.stringify(workflow)) as WorkflowJSON;
}

function workflowSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  return JSON.stringify(workflow);
}

function edgeId(edge: MicroflowWorkflowEdgeJSON | WorkflowEdgeJSON): string | undefined {
  const data = (edge as MicroflowWorkflowEdgeJSON).data as Partial<FlowGramMicroflowEdgeData> | undefined;
  return data?.flowId ?? (edge as { id?: string }).id;
}

function edgeKey(edge: MicroflowWorkflowEdgeJSON | WorkflowEdgeJSON): string {
  return [
    String(edge.sourceNodeID ?? ""),
    String(edge.sourcePortID ?? ""),
    String(edge.targetNodeID ?? ""),
    String(edge.targetPortID ?? ""),
  ].join("::");
}

function createEdgeDataByLineKey(workflow: WorkflowJSON | MicroflowWorkflowJSON): ReadonlyMap<string, FlowGramMicroflowEdgeData> {
  const map = new Map<string, FlowGramMicroflowEdgeData>();
  const normalized = normalizeWorkflow(workflow);
  for (const edge of (normalized.edges ?? []) as MicroflowWorkflowEdgeJSON[]) {
    const data = edge.data as FlowGramMicroflowEdgeData | undefined;
    if (data?.flowId) {
      map.set(edgeKey(edge), data);
    }
  }
  return map;
}

function edgeStructureSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  const stableWorkflow = stripTransientWorkflowState(workflow);
  return JSON.stringify(
    ((stableWorkflow.edges ?? []) as Array<WorkflowEdgeJSON | MicroflowWorkflowEdgeJSON>)
      .map(edge => ({
        id: edgeId(edge),
        key: edgeKey(edge),
        data: (edge as MicroflowWorkflowEdgeJSON).data,
      }))
      .sort((a, b) => String(a.id ?? a.key).localeCompare(String(b.id ?? b.key))),
  );
}

function nodeStructureSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  const stableWorkflow = stripTransientWorkflowState(workflow);
  return JSON.stringify(
    ((stableWorkflow.nodes ?? []) as MicroflowWorkflowNodeJSON[])
      .map(node => ({ id: node.id, type: node.type, data: node.data }))
      .sort((a, b) => String(a.id).localeCompare(String(b.id))),
  );
}

function workflowRenderStructureKey(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  return JSON.stringify({
    nodes: ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[])
      .map(node => ({ id: node.id, type: node.type }))
      .sort((a, b) => String(a.id).localeCompare(String(b.id))),
    edges: ((workflow.edges ?? []) as Array<WorkflowEdgeJSON | MicroflowWorkflowEdgeJSON>)
      .map(edge => ({ id: edgeId(edge), key: edgeKey(edge) }))
      .sort((a, b) => String(a.id ?? a.key).localeCompare(String(b.id ?? b.key))),
  });
}

function ensureNodeData(node: MicroflowWorkflowNodeJSON): MicroflowWorkflowNodeJSON {
  const objectKind = String((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type);
  const data = node.data as Partial<FlowGramMicroflowNodeData> | undefined;
  const actionKind = data?.actionKind;
  return {
    ...node,
    type: objectKind,
    data: {
      ...data,
      objectId: node.id,
      objectKind,
      collectionId: data?.collectionId ?? String(node.meta?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID),
      title: data?.title ?? objectKind,
      subtitle: data?.subtitle ?? data?.officialType ?? objectKind,
      officialType: data?.officialType ?? objectKind,
      disabled: data?.disabled ?? false,
      validationState: data?.validationState ?? "valid",
      runtimeState: data?.runtimeState ?? "idle",
      issueCount: data?.issueCount ?? 0,
      usageSourceHighlight: data?.usageSourceHighlight ?? false,
      usageConsumerHighlight: data?.usageConsumerHighlight ?? false,
    },
    meta: {
      ...node.meta,
      position: {
        x: Number(node.meta?.position?.x ?? 0),
        y: Number(node.meta?.position?.y ?? 0),
      },
      size: node.meta?.size ?? getMendixMicroflowNodeSize(objectKind),
      collectionId: node.meta?.collectionId ?? data?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID,
      parentObjectId: node.meta?.parentObjectId ?? data?.parentObjectId,
      nodeDTOType: objectKind,
      useDynamicPort: true,
      defaultPorts: node.meta?.defaultPorts?.length
        ? node.meta.defaultPorts
        : flowGramPortsForObjectKind(objectKind as FlowGramMicroflowNodeData["objectKind"], actionKind),
    },
  };
}

function ensureEdgeData(edge: MicroflowWorkflowEdgeJSON, index: number): MicroflowWorkflowEdgeJSON {
  const id = edgeId(edge) ?? `flow-${index + 1}`;
  const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
  return {
    ...edge,
    id,
    data: {
      ...data,
      flowId: id,
      flowKind: data?.flowKind ?? "sequence",
      edgeKind: data?.edgeKind ?? "sequence",
      isErrorHandler: data?.isErrorHandler ?? false,
      caseValues: data?.caseValues ?? [],
      lineKind: forceOrthogonalLineKind(data?.lineKind),
      validationState: data?.validationState ?? "valid",
      runtimeState: data?.runtimeState ?? "idle",
    },
  };
}

function workflowNodesOverlap(
  a: MicroflowWorkflowNodeJSON,
  b: MicroflowWorkflowNodeJSON,
  gap = MICROFLOW_GRID_SIZE,
): boolean {
  const aPosition = a.meta?.position ?? { x: 0, y: 0 };
  const bPosition = b.meta?.position ?? { x: 0, y: 0 };
  const aSize = a.meta?.size ?? getMendixMicroflowNodeSize((a.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? a.type);
  const bSize = b.meta?.size ?? getMendixMicroflowNodeSize((b.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? b.type);
  return Math.abs(aPosition.x - bPosition.x) < (aSize.width + bSize.width) / 2 + gap
    && Math.abs(aPosition.y - bPosition.y) < (aSize.height + bSize.height) / 2 + gap;
}

function resolveWorkflowNodeOverlaps(
  nodes: MicroflowWorkflowNodeJSON[],
  options: { snapToGrid?: boolean; preferredNodeIds?: string[] } = {},
): MicroflowWorkflowNodeJSON[] {
  const preferred = new Set(options.preferredNodeIds ?? []);
  const ordered = [
    ...nodes.filter(node => preferred.has(node.id)),
    ...nodes.filter(node => !preferred.has(node.id)),
  ];
  const resolved = new Map<string, MicroflowWorkflowNodeJSON>();
  const stepX = MICROFLOW_GRID_SIZE * 3;
  const stepY = MICROFLOW_GRID_SIZE * 2;

  for (const current of ordered) {
    const parentObjectId = String(current.meta?.parentObjectId ?? current.data?.parentObjectId ?? "");
    const siblings = [...resolved.values()].filter(node => String(node.meta?.parentObjectId ?? node.data?.parentObjectId ?? "") === parentObjectId);
    let candidate = current;
    let attempts = 0;
    while (siblings.some(node => workflowNodesOverlap(candidate, node)) && attempts < 32) {
      const position = candidate.meta?.position ?? { x: 0, y: 0 };
      const ring = Math.floor(attempts / 4) + 1;
      const direction = attempts % 4;
      const nextPosition = direction === 0
        ? { x: position.x + stepX * ring, y: position.y }
        : direction === 1
          ? { x: position.x, y: position.y + stepY * ring }
          : direction === 2
            ? { x: position.x - stepX * ring, y: position.y }
            : { x: position.x, y: position.y - stepY * ring };
      candidate = {
        ...candidate,
        meta: {
          ...candidate.meta,
          position: options.snapToGrid === false ? nextPosition : snapMicroflowPoint(nextPosition),
        },
      };
      attempts += 1;
    }
    resolved.set(candidate.id, candidate);
  }

  return nodes.map(node => resolved.get(node.id) ?? node);
}

function normalizeWorkflow(
  workflow: WorkflowJSON | MicroflowWorkflowJSON,
  options: { snapToGrid?: boolean; preferredNodeIds?: string[] } = {},
): WorkflowJSON {
  const flatWorkflow = flattenFlowGramWorkflowForPersistence(workflow);
  const normalizedNodes = ((flatWorkflow.nodes ?? []) as MicroflowWorkflowNodeJSON[]).map(node => {
    const normalized = ensureNodeData(node);
    if (!options.snapToGrid || !normalized.meta?.position) {
      return normalized;
    }
    return {
      ...normalized,
      meta: {
        ...normalized.meta,
        position: {
          x: Math.round(normalized.meta.position.x / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
          y: Math.round(normalized.meta.position.y / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
        },
      },
    };
  });
  const nodes = resolveWorkflowNodeOverlaps(normalizedNodes, options);
  return {
    ...flatWorkflow,
    nodes: nodes as WorkflowJSON["nodes"],
    edges: normalizeMicroflowDesignEdges({ ...flatWorkflow, nodes } as WorkflowJSON).map(ensureEdgeData) as WorkflowJSON["edges"],
  };
}

function graphBounds(workflow: MicroflowWorkflowJSON | WorkflowJSON) {
  const nodes = (workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[];
  if (nodes.length === 0) {
    return undefined;
  }
  const boxes = nodes.map(node => {
    const position = node.meta?.position ?? { x: 0, y: 0 };
    const size = node.meta?.size ?? { width: 160, height: 76 };
    return {
      left: position.x - size.width / 2,
      top: position.y - size.height / 2,
      right: position.x + size.width / 2,
      bottom: position.y + size.height / 2,
    };
  });
  const minX = Math.min(...boxes.map(box => box.left));
  const minY = Math.min(...boxes.map(box => box.top));
  const maxX = Math.max(...boxes.map(box => box.right));
  const maxY = Math.max(...boxes.map(box => box.bottom));
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: Math.max(1, maxX - minX),
    height: Math.max(1, maxY - minY),
  };
}

function fitViewportForWorkflow(workflow: MicroflowWorkflowJSON | WorkflowJSON, rect: Pick<DOMRect, "width" | "height">): MicroflowDesignSchema["editor"]["viewport"] {
  const bounds = graphBounds(workflow);
  if (!bounds) {
    return { x: 0, y: 0, zoom: 1 };
  }
  const zoom = Math.max(0.2, Math.min(1.2, Math.min((rect.width - 120) / bounds.width, (rect.height - 120) / bounds.height)));
  return {
    x: Math.round((bounds.minX + bounds.width / 2) * zoom - rect.width / 2),
    y: Math.round((bounds.minY + bounds.height / 2) * zoom - rect.height / 2),
    zoom,
  };
}

function centerViewportForWorkflow(
  workflow: MicroflowWorkflowJSON | WorkflowJSON,
  rect: Pick<DOMRect, "width" | "height">,
  currentZoom: number,
): MicroflowDesignSchema["editor"]["viewport"] {
  const bounds = graphBounds(workflow);
  const zoom = Math.max(0.2, Math.min(2, currentZoom || 1));
  if (!bounds) {
    return { x: 0, y: 0, zoom };
  }
  return {
    x: Math.round((bounds.minX + bounds.width / 2) * zoom - rect.width / 2),
    y: Math.round((bounds.minY + bounds.height / 2) * zoom - rect.height / 2),
    zoom,
  };
}

function findStartNodeForViewport(workflow: MicroflowWorkflowJSON | WorkflowJSON): MicroflowWorkflowNodeJSON | undefined {
  const nodes = (workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[];
  return nodes.find(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "startEvent") ?? nodes[0];
}

function initialViewportFromStartNode(workflow: MicroflowWorkflowJSON | WorkflowJSON, rect: Pick<DOMRect, "width" | "height">): MicroflowDesignSchema["editor"]["viewport"] {
  const startNode = findStartNodeForViewport(workflow);
  if (!startNode) {
    return { x: 0, y: 0, zoom: INITIAL_START_VIEWPORT_ZOOM };
  }
  const position = startNode.meta?.position ?? { x: 0, y: 0 };
  const zoom = INITIAL_START_VIEWPORT_ZOOM;
  return {
    x: Math.round(Math.max(0, position.x * zoom - INITIAL_START_VIEWPORT_LEFT_PADDING)),
    y: Math.round(Math.max(0, position.y * zoom - rect.height * INITIAL_START_VIEWPORT_TOP_RATIO)),
    zoom,
  };
}

function lineMatchesEdge(line: WorkflowLineEntity, edge: WorkflowEdgeJSON): boolean {
  const candidate = line as DisposableLineCandidate;
  const json = (line as { toJSON?: () => DisposableLineSnapshot }).toJSON?.();
  const candidateId = edgeId(edge as MicroflowWorkflowEdgeJSON);
  if (
    candidateId
    && (String(candidate.id) === candidateId
      || String(json?.id) === candidateId
      || candidate.data?.flowId === candidateId
      || json?.data?.flowId === candidateId)
  ) {
    return true;
  }
  return Boolean(
    String(candidate.info?.from ?? json?.sourceNodeID ?? "") === String(edge.sourceNodeID ?? "")
      && String(candidate.info?.to ?? json?.targetNodeID ?? "") === String(edge.targetNodeID ?? "")
      && String(candidate.info?.fromPort ?? json?.sourcePortID ?? "") === String(edge.sourcePortID ?? "")
      && String(candidate.info?.toPort ?? json?.targetPortID ?? "") === String(edge.targetPortID ?? ""),
  );
}

function edgeIsBusinessValid(workflow: WorkflowJSON, edge: WorkflowEdgeJSON): boolean {
  return isMicroflowDesignEdgeBusinessValid(workflow, edge);
}

type GraphNodePortLike = {
  id: string;
  direction: "input" | "output";
  position?: { x: number; y: number };
};

type GraphNodeLike = {
  objectId: string;
  position: { x: number; y: number };
  size: { width: number; height: number };
  ports: GraphNodePortLike[];
};

type EdgeReconnectGeometry = {
  flowId: string;
  edgeKind: string;
  sourceObjectId: string;
  sourcePortId: string;
  sourcePoint: { x: number; y: number };
  targetObjectId: string;
  targetPortId: string;
  targetPoint: { x: number; y: number };
};

type GraphPortGeometry = {
  id: string;
  objectId: string;
  direction: "input" | "output";
  point: { x: number; y: number };
};

function absolutePortPosition(node: GraphNodeLike, port?: GraphNodePortLike): { x: number; y: number } {
  const relative = port?.position ?? {
    x: port?.direction === "input" ? 0 : node.size.width,
    y: node.size.height / 2,
  };
  return {
    x: node.position.x + relative.x,
    y: node.position.y + relative.y,
  };
}

function buildReconnectGeometry(schema: MicroflowDesignSchema): {
  edgeByFlowId: Map<string, EdgeReconnectGeometry>;
  ports: GraphPortGeometry[];
} {
  const graph = toEditorGraph(schema as unknown as Parameters<typeof toEditorGraph>[0]);
  const nodeByObjectId = new Map<string, GraphNodeLike>(
    graph.nodes.map(node => [String(node.objectId), node]),
  );
  const ports: GraphPortGeometry[] = [];
  for (const node of graph.nodes) {
    const nodeLike = nodeByObjectId.get(String(node.objectId));
    if (!nodeLike) {
      continue;
    }
    for (const port of node.ports) {
      ports.push({
        id: port.id,
        objectId: String(node.objectId),
        direction: port.direction,
        point: absolutePortPosition(nodeLike, port),
      });
    }
  }
  const edgeByFlowId = new Map<string, EdgeReconnectGeometry>();
  for (const edge of graph.edges) {
    const source = nodeByObjectId.get(String(edge.sourceNodeId).replace(/^node-/, ""));
    const target = nodeByObjectId.get(String(edge.targetNodeId).replace(/^node-/, ""));
    if (!source || !target) {
      continue;
    }
    const sourcePort = source.ports.find(port => port.id === edge.sourcePortId) ?? source.ports.find(port => port.direction === "output");
    const targetPort = target.ports.find(port => port.id === edge.targetPortId) ?? target.ports.find(port => port.direction === "input");
    if (!sourcePort || !targetPort || !edge.flowId) {
      continue;
    }
    edgeByFlowId.set(edge.flowId, {
      flowId: edge.flowId,
      edgeKind: edge.edgeKind,
      sourceObjectId: String(source.objectId),
      sourcePortId: sourcePort.id,
      sourcePoint: absolutePortPosition(source, sourcePort),
      targetObjectId: String(target.objectId),
      targetPortId: targetPort.id,
      targetPoint: absolutePortPosition(target, targetPort),
    });
  }
  return { edgeByFlowId, ports };
}

function nearestReconnectPortCandidate(
  ports: GraphPortGeometry[],
  point: { x: number; y: number },
  direction: "input" | "output",
  radius = 16,
): GraphPortGeometry | undefined {
  let nearest: { item: GraphPortGeometry; distance: number } | undefined;
  for (const port of ports) {
    if (port.direction !== direction) {
      continue;
    }
    const distance = Math.hypot(point.x - port.point.x, point.y - port.point.y);
    if (distance <= radius && (!nearest || distance < nearest.distance)) {
      nearest = { item: port, distance };
    }
  }
  return nearest?.item;
}

function findNearestDropInsertFlowId(
  schema: MicroflowDesignSchema,
  point: { x: number; y: number },
  threshold = 24,
): string | undefined {
  const graph = toEditorGraph(schema as unknown as Parameters<typeof toEditorGraph>[0]);
  const nodeById = new Map<string, GraphNodeLike>(
    graph.nodes.map(node => [String(node.objectId), node]),
  );
  const candidates: Array<{
    flowId: string;
    edgeKind?: string;
    sourcePoint: { x: number; y: number };
    targetPoint: { x: number; y: number };
  }> = [];
  for (const edge of graph.edges) {
    const source = nodeById.get(String(edge.sourceNodeId).replace(/^node-/, ""));
    const target = nodeById.get(String(edge.targetNodeId).replace(/^node-/, ""));
    if (!source || !target || !edge.flowId) {
      continue;
    }
    const sourcePort = source.ports.find(port => port.id === edge.sourcePortId) ?? source.ports.find(port => port.direction === "output");
    const targetPort = target.ports.find(port => port.id === edge.targetPortId) ?? target.ports.find(port => port.direction === "input");
    candidates.push({
      flowId: edge.flowId,
      edgeKind: edge.edgeKind,
      sourcePoint: absolutePortPosition(source, sourcePort),
      targetPoint: absolutePortPosition(target, targetPort),
    });
  }
  return findNearestInsertableEdgeFlowId(candidates, point, threshold);
}

function resolveFlowInsertAnchor(schema: MicroflowDesignSchema, flowId: string): MicroflowPoint | undefined {
  const graph = toEditorGraph(schema as unknown as Parameters<typeof toEditorGraph>[0]);
  const edge = graph.edges.find(item => item.flowId === flowId);
  if (!edge) {
    return undefined;
  }
  const nodeById = new Map<string, GraphNodeLike>(
    graph.nodes.map(node => [String(node.objectId), node]),
  );
  const source = nodeById.get(String(edge.sourceNodeId).replace(/^node-/, ""));
  const target = nodeById.get(String(edge.targetNodeId).replace(/^node-/, ""));
  if (!source || !target) {
    return undefined;
  }
  const sourcePort = source.ports.find(port => port.id === edge.sourcePortId) ?? source.ports.find(port => port.direction === "output");
  const targetPort = target.ports.find(port => port.id === edge.targetPortId) ?? target.ports.find(port => port.direction === "input");
  const sourcePoint = absolutePortPosition(source, sourcePort);
  const targetPoint = absolutePortPosition(target, targetPort);
  return {
    x: Math.round((sourcePoint.x + targetPoint.x) / 2),
    y: Math.round((sourcePoint.y + targetPoint.y) / 2),
  };
}

function findInvalidBusinessEdge(workflow: WorkflowJSON): WorkflowEdgeJSON | undefined {
  return [...((workflow.edges ?? []) as WorkflowEdgeJSON[])].reverse().find(edge => !edgeIsBusinessValid(workflow, edge));
}

function selectionFromIds(workflow: MicroflowWorkflowJSON, ids: string[]): FlowGramMicroflowSelection {
  const workflowNodes = workflow.nodes as MicroflowWorkflowNodeJSON[];
  const resolveObjectId = (id: string): string | undefined => {
    const direct = workflowNodeById(workflow, id);
    if (direct) {
      return String((direct.data as { objectId?: string } | undefined)?.objectId ?? direct.id);
    }
    const byObjectId = workflowNodes.find(node => String((node.data as { objectId?: string } | undefined)?.objectId ?? "") === id);
    if (byObjectId) {
      return String((byObjectId.data as { objectId?: string } | undefined)?.objectId ?? byObjectId.id);
    }
    if (id.startsWith("node-")) {
      return resolveObjectId(id.slice("node-".length));
    }
    const prefixed = workflowNodeById(workflow, `node-${id}`);
    if (prefixed) {
      return String((prefixed.data as { objectId?: string } | undefined)?.objectId ?? prefixed.id);
    }
    return undefined;
  };
  const objectIds = ids
    .map(resolveObjectId)
    .filter((id): id is string => Boolean(id));
  const flowIds = ids
    .map(id => {
      const edge = workflowEdgeById(workflow, id);
      return edge ? edgeId(edge) : undefined;
    })
    .filter((id): id is string => Boolean(id));
  const count = objectIds.length + flowIds.length;
  return {
    objectId: objectIds[0],
    flowId: objectIds.length === 0 ? flowIds[0] : undefined,
    collectionId: objectIds[0] ? MICROFLOW_ROOT_COLLECTION_ID : undefined,
    objectIds,
    flowIds,
    mode: count === 0 ? "none" : count === 1 ? "single" : "multi",
  };
}

function selectionFromTarget(target: HTMLElement | undefined, workflow: MicroflowWorkflowJSON): FlowGramMicroflowSelection | undefined {
  const edgeElement = target?.closest<HTMLElement>("[data-flow-id]");
  if (edgeElement) {
    const flowId = edgeElement.dataset.flowId;
    if (flowId) {
      return { objectId: undefined, flowId, collectionId: undefined, objectIds: [], flowIds: [flowId], mode: "single" };
    }
  }
  const nodeElement = target?.closest<HTMLElement>(".microflow-flowgram-node[data-microflow-object-id]");
  if (!nodeElement) {
    return undefined;
  }
  const objectId = nodeElement.dataset.microflowObjectId;
  const workflowNodes = workflow.nodes as MicroflowWorkflowNodeJSON[];
  const hasNode = Boolean(
    objectId
    && (
      workflowNodeById(workflow, objectId)
      || workflowNodeById(workflow, `node-${objectId}`)
      || workflowNodes.find(node => String((node.data as { objectId?: string } | undefined)?.objectId ?? "") === objectId)
    ),
  );
  if (!objectId || !hasNode) {
    return undefined;
  }
  return { objectId, flowId: undefined, collectionId: MICROFLOW_ROOT_COLLECTION_ID, objectIds: [objectId], flowIds: [], mode: "single" };
}

function FlowGramMicroflowNativeMiniMap(props: { schema: MicroflowDesignSchema; onFocusNode: (objectId: string) => void }) {
  const nodes = props.schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  const edges = props.schema.workflow.edges as MicroflowWorkflowEdgeJSON[];
  const bounds = graphBounds(props.schema.workflow) ?? { minX: 0, minY: 0, width: 1, height: 1 };
  const viewBox = `${bounds.minX - 80} ${bounds.minY - 80} ${bounds.width + 160} ${bounds.height + 160}`;
  const nodeById = new Map(nodes.map(node => [node.id, node]));
  const viewport = props.schema.editor.viewport;
  const viewportWidth = 520 / Math.max(0.2, viewport.zoom);
  const viewportHeight = 320 / Math.max(0.2, viewport.zoom);

  return (
    <div className="microflow-flowgram-minimap" aria-label="Microflow minimap">
      <svg viewBox={viewBox} role="img">
        {edges.map(edge => {
          const source = nodeById.get(edge.sourceNodeID);
          const target = nodeById.get(edge.targetNodeID);
          if (!source || !target) {
            return null;
          }
          const sourcePosition = source.meta?.position ?? { x: 0, y: 0 };
          const targetPosition = target.meta?.position ?? { x: 0, y: 0 };
          return (
            <line
              key={edgeId(edge)}
              x1={sourcePosition.x}
              y1={sourcePosition.y}
              x2={targetPosition.x}
              y2={targetPosition.y}
              className="microflow-flowgram-minimap-edge microflow-flowgram-minimap-edge-sequence"
            />
          );
        })}
        {nodes.map(node => {
          const position = node.meta?.position ?? { x: 0, y: 0 };
          const size = node.meta?.size ?? { width: 160, height: 76 };
          const objectKind = (node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type;
          return (
            <rect
              key={node.id}
              x={position.x - size.width / 2}
              y={position.y - size.height / 2}
              width={size.width}
              height={size.height}
              rx={objectKind === "exclusiveSplit" || objectKind === "inheritanceSplit" ? 4 : 10}
              className={`microflow-flowgram-minimap-node microflow-flowgram-minimap-node-${objectKind}`}
              role="button"
              tabIndex={0}
              onClick={() => props.onFocusNode(node.id)}
              onKeyDown={event => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  props.onFocusNode(node.id);
                }
              }}
            />
          );
        })}
        <rect
          x={viewport.x / Math.max(0.2, viewport.zoom)}
          y={viewport.y / Math.max(0.2, viewport.zoom)}
          width={viewportWidth}
          height={viewportHeight}
          className="microflow-flowgram-minimap-viewport"
        />
      </svg>
    </div>
  );
}

function FlowGramMicroflowNativeCanvasInner(props: FlowGramMicroflowNativeCanvasInnerProps) {
  const playground = usePlayground();
  const showBuiltInToolbar = props.showBuiltInToolbar ?? false;
  const playgroundRef = useRef(playground);
  playgroundRef.current = playground;
  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const dragService = useService<WorkflowDragService>(WorkflowDragService);
  const linesManager = useService<WorkflowLinesManager>(WorkflowLinesManager);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const selectorBoxConfig = useEntity<SelectorBoxConfigEntity>(SelectorBoxConfigEntity);
  const containerRef = useRef<HTMLDivElement>(null);
  const propsRef = useRef(props);
  const latestSchemaRef = useRef(props.schema);
  const reloadingRef = useRef(false);
  const draggingRef = useRef(false);
  const lastWorkflowSignatureRef = useRef<string>();
  const localViewportInitRef = useRef(false);
  // 优先使用外层组件传入的持久化 ref，避免 structureKey remount 后 viewport 重置
  const initialViewportFitDoneRef = props.viewportInitializedRef ?? localViewportInitRef;
  const [dropActive, setDropActive] = useState(false);
  const [dropPreview, setDropPreview] = useState<{
    position: MicroflowPoint;
    size: { width: number; height: number };
    valid: boolean;
    insertFlowId?: string;
    insertAnchor?: MicroflowPoint;
  }>();
  const [internalPanToolActive, setInternalPanToolActive] = useState(false);
  const panToolControlled = props.canvasPanToolActive !== undefined;
  const [canvasNodeToolbar, setCanvasNodeToolbar] = useState<{ x: number; y: number; objectId: string } | undefined>();
  const [hoveredFlowId, setHoveredFlowId] = useState<string>();
  const [reconnectState, setReconnectState] = useState<{
    flowId: string;
    dragEndpoint: ReconnectFlowDragEndpoint;
    pointer: { x: number; y: number };
    fixedPoint: { x: number; y: number };
    fixedPortId: string;
    fixedObjectId: string;
    originalPoint: { x: number; y: number };
    originalPortId: string;
    originalObjectId: string;
    candidate?: {
      objectId: string;
      portId: string;
      point: { x: number; y: number };
      allowed: boolean;
      message?: string;
    };
    failedAt?: number;
  }>();
  const [portFlashState, setPortFlashState] = useState<{ portId: string; tick: number }>();
  const panToolActive = panToolControlled ? Boolean(props.canvasPanToolActive) : internalPanToolActive;
  const togglePanTool = () => {
    const next = !panToolActive;
    if (panToolControlled) {
      props.onCanvasPanToolChange?.(next);
    } else {
      setInternalPanToolActive(next);
    }
  };
  const [spacePressed, setSpacePressed] = useState(false);
  const [isViewportPanGrabbing, setIsViewportPanGrabbing] = useState(false);
  const panToolActiveRef = useRef(false);
  const spacePressedRef = useRef(false);
  const viewportPanPointerIdRef = useRef<number | null>(null);
  const viewportPanOriginRef = useRef<{
    clientX: number;
    clientY: number;
    viewportX: number;
    viewportY: number;
    zoom: number;
  } | null>(null);
  const viewportPanButtonRef = useRef<number | null>(null);
  const viewportPanMovedRef = useRef(false);
  const suppressNextContextMenuRef = useRef(false);
  const dragStartPosRef = useRef<{ x: number; y: number } | null>(null);
  const edgeHoverTimerRef = useRef<number | undefined>();
  const edgeHoverCandidateRef = useRef<string | undefined>();
  const userViewportPanningRef = useRef(false);
  const lastSyncedViewportRef = useRef<{ x: number; y: number; zoom: number } | null>(null);
  propsRef.current = props;
  latestSchemaRef.current = props.schema;
  panToolActiveRef.current = panToolActive;
  spacePressedRef.current = spacePressed;

  const decoratedWorkflow = useMemo(
    () => decorateWorkflow({
      schema: props.schema,
      validationIssues: props.validationIssues,
      runtimeTrace: props.runtimeTrace,
      runtimeOverlay: props.runtimeOverlay,
      loopIteration: props.loopIteration,
      pausedNodeIds: props.pausedNodeIds,
      breakpointNodeIds: props.breakpointNodeIds,
      conditionalBreakpointNodeIds: props.conditionalBreakpointNodeIds,
      nodeViewModes: props.nodeViewModes,
      usageHighlights: props.usageHighlights,
    }),
    [
      props.pausedNodeIds,
      props.breakpointNodeIds,
      props.conditionalBreakpointNodeIds,
      props.nodeViewModes,
      props.loopIteration,
      props.runtimeTrace,
      props.runtimeOverlay,
      props.schema,
      props.usageHighlights,
      props.validationIssues,
    ],
  );
  const renderedWorkflow = useMemo(
    () => nestLoopChildrenForFlowGram(decoratedWorkflow),
    [decoratedWorkflow],
  );
  const gridEnabled = props.schema.editor.gridEnabled !== false;
  const miniMapVisible = props.schema.editor.showMiniMap === true;
  const microflowComplexity = useMemo(
    () => summarizeMicroflowComplexity(props.schema),
    [props.schema],
  );
  const reconnectGeometry = useMemo(
    () => buildReconnectGeometry(props.schema),
    [props.schema],
  );
  const hoveredEdgeGeometry = hoveredFlowId ? reconnectGeometry.edgeByFlowId.get(hoveredFlowId) : undefined;
  useEffect(() => {
    if (reconnectState && !reconnectGeometry.edgeByFlowId.has(reconnectState.flowId)) {
      setReconnectState(undefined);
    }
  }, [reconnectGeometry.edgeByFlowId, reconnectState]);
  useEffect(() => {
    const canvas = containerRef.current;
    if (!canvas) {
      return;
    }
    const activeFlowId = dropPreview?.insertFlowId;
    const edgeElements = canvas.querySelectorAll<HTMLElement>(".gedit-flow-activity-edge");
    edgeElements.forEach(edgeElement => {
      const matches = Boolean(activeFlowId) && edgeElement.getAttribute("data-flow-id") === activeFlowId;
      edgeElement.classList.toggle("microflow-flowgram-edge-drop-target", matches);
    });
    return () => {
      edgeElements.forEach(edgeElement => {
        edgeElement.classList.remove("microflow-flowgram-edge-drop-target");
      });
    };
  }, [dropPreview?.insertFlowId, renderedWorkflow]);

  useEffect(() => {
    const canvas = containerRef.current;
    if (!canvas) {
      return;
    }
    const edgeElements = canvas.querySelectorAll<HTMLElement>(".gedit-flow-activity-edge");
    edgeElements.forEach(edgeElement => {
      const flowId = edgeElement.getAttribute("data-flow-id");
      const isHovered = Boolean(flowId && hoveredFlowId && flowId === hoveredFlowId && !reconnectState);
      const isReconnecting = Boolean(flowId && reconnectState?.flowId && flowId === reconnectState.flowId);
      edgeElement.classList.toggle("microflow-flowgram-edge-hovered", isHovered);
      edgeElement.classList.toggle("microflow-flowgram-edge-reconnecting", isReconnecting);
      edgeElement.classList.toggle("microflow-flowgram-edge-reconnect-failed", Boolean(isReconnecting && reconnectState?.failedAt));
    });
    return () => {
      edgeElements.forEach(edgeElement => {
        edgeElement.classList.remove("microflow-flowgram-edge-hovered");
        edgeElement.classList.remove("microflow-flowgram-edge-reconnecting");
        edgeElement.classList.remove("microflow-flowgram-edge-reconnect-failed");
      });
    };
  }, [hoveredFlowId, reconnectState]);

  // 将选中连线的 is-selected CSS 类同步到 .gedit-flow-activity-edge DOM 元素
  const selectedFlowId = props.schema.editor.selection?.flowId;
  useEffect(() => {
    const canvas = containerRef.current;
    if (!canvas) {
      return;
    }
    const edgeElements = canvas.querySelectorAll<HTMLElement>(".gedit-flow-activity-edge");
    edgeElements.forEach(edgeElement => {
      const flowId = edgeElement.getAttribute("data-flow-id");
      edgeElement.classList.toggle("is-selected", Boolean(flowId && selectedFlowId && flowId === selectedFlowId));
    });
    return () => {
      edgeElements.forEach(edgeElement => {
        edgeElement.classList.remove("is-selected");
      });
    };
  }, [selectedFlowId, renderedWorkflow]);

  useEffect(() => {
    const canvas = containerRef.current;
    if (!canvas) {
      return;
    }
    const tagged = new Set<HTMLElement>();
    const candidatePortId = reconnectState?.candidate?.portId;
    if (candidatePortId) {
      const candidateEl = canvas.querySelector<HTMLElement>(`[data-port-id="${candidatePortId}"]`);
      if (candidateEl) {
        candidateEl.classList.add(reconnectState?.candidate?.allowed ? "microflow-flowgram-port-reconnect-valid" : "microflow-flowgram-port-reconnect-invalid");
        tagged.add(candidateEl);
      }
    }
    const fixedEl = reconnectState?.fixedPortId
      ? canvas.querySelector<HTMLElement>(`[data-port-id="${reconnectState.fixedPortId}"]`)
      : undefined;
    if (fixedEl) {
      fixedEl.classList.add("microflow-flowgram-port-reconnect-pending-disconnect");
      tagged.add(fixedEl);
    }
    if (portFlashState?.portId) {
      const flashEl = canvas.querySelector<HTMLElement>(`[data-port-id="${portFlashState.portId}"]`);
      if (flashEl) {
        flashEl.classList.add("microflow-flowgram-port-reconnect-flash");
        tagged.add(flashEl);
      }
    }
    // 拖拽重连时，高亮所有方向匹配且不属于固定端节点的可用端口
    if (reconnectState) {
      const availableDirection = reconnectState.dragEndpoint === "source" ? "output" : "input";
      for (const port of reconnectGeometry.ports) {
        if (port.direction !== availableDirection) continue;
        if (port.objectId === reconnectState.fixedObjectId) continue;
        const el = canvas.querySelector<HTMLElement>(`[data-port-id="${port.id}"]`);
        if (el && !tagged.has(el)) {
          el.classList.add("microflow-flowgram-port-available");
          tagged.add(el);
        }
      }
    }
    return () => {
      for (const el of tagged) {
        el.classList.remove("microflow-flowgram-port-reconnect-valid");
        el.classList.remove("microflow-flowgram-port-reconnect-invalid");
        el.classList.remove("microflow-flowgram-port-reconnect-pending-disconnect");
        el.classList.remove("microflow-flowgram-port-reconnect-flash");
        el.classList.remove("microflow-flowgram-port-available");
      }
    };
  }, [portFlashState, reconnectState, reconnectGeometry]);

  useLayoutEffect(() => {
    selectorBoxConfig.disabled = panToolActive || spacePressed;
    return () => {
      selectorBoxConfig.disabled = false;
    };
  }, [panToolActive, selectorBoxConfig, spacePressed]);

  useEffect(() => () => {
    clearEdgeHoverTimer();
  }, []);

  const commitWorkflow = (workflow: WorkflowJSON, reason: string, options: { snapToGrid?: boolean; preferredNodeIds?: string[] } = {}) => {
    const nextWorkflow = stripTransientWorkflowState(normalizeWorkflow(workflow, options));
    const nextSignature = workflowSignature(nextWorkflow);
    if (nextSignature === workflowSignature(stripTransientWorkflowState(latestSchemaRef.current.workflow))) {
      return;
    }
    const nextSchema: MicroflowDesignSchema = {
      ...latestSchemaRef.current,
      workflow: nextWorkflow as MicroflowWorkflowJSON,
      audit: {
        ...latestSchemaRef.current.audit,
        updatedAt: new Date().toISOString(),
      },
    };
    latestSchemaRef.current = nextSchema;
    lastWorkflowSignatureRef.current = nextSignature;
    propsRef.current.onSchemaChange(nextSchema, reason);
  };

  const commitEdgeStructure = (workflow: WorkflowJSON, reason: string) => {
    const nextWorkflow = stripTransientWorkflowState(normalizeWorkflow({
      ...latestSchemaRef.current.workflow,
      edges: workflow.edges ?? [],
    } as WorkflowJSON));
    const nextSignature = workflowSignature(nextWorkflow);
    if (nextSignature === workflowSignature(stripTransientWorkflowState(latestSchemaRef.current.workflow))) {
      return;
    }
    const nextSchema: MicroflowDesignSchema = {
      ...latestSchemaRef.current,
      workflow: nextWorkflow as MicroflowWorkflowJSON,
      audit: {
        ...latestSchemaRef.current.audit,
        updatedAt: new Date().toISOString(),
      },
    };
    latestSchemaRef.current = nextSchema;
    lastWorkflowSignatureRef.current = nextSignature;
    propsRef.current.onSchemaChange(nextSchema, reason);
  };

  const disposeTemporaryEdgeLine = (edge: WorkflowEdgeJSON) => {
    const line = linesManager.getAllLines().find(item => lineMatchesEdge(item, edge));
    line?.dispose();
    return Boolean(line);
  };

  const emitCanvasZoomChange = useCallback((zoom: number) => {
    if (typeof window === "undefined") {
      return;
    }
    const normalizedZoom = Number.isFinite(zoom) ? zoom : 1;
    const percent = Math.round(Math.max(0, normalizedZoom) * 100);
    window.dispatchEvent(new CustomEvent("canvas:zoom-change", { detail: { zoom: percent } }));
  }, []);

  const applyViewportZoomFromCanvasCenter = useCallback((normalizedZoom: number) => {
    const root = containerRef.current;
    const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const rect = root?.getBoundingClientRect();
    const next = microflowZoomViewportAtCanvasCenter(v, rect?.width ?? 0, rect?.height ?? 0, normalizedZoom);
    const cfg = playground.config as unknown as FlowGramPlaygroundViewportConfig;
    if (typeof cfg.zoom === "function") {
      cfg.zoom(next.zoom);
    }
    cfg.updateConfig?.({ zoom: next.zoom, scrollX: next.x, scrollY: next.y });
    lastSyncedViewportRef.current = next;
    propsRef.current.onViewportChange?.(next);
    emitCanvasZoomChange(next.zoom);
  }, [emitCanvasZoomChange, playground]);

  const pushViewportToPlaygroundConfig = useCallback((viewport: MicroflowDesignSchema["editor"]["viewport"], options?: { force?: boolean }) => {
    const v = viewport ?? { x: 0, y: 0, zoom: 1 };
    const last = lastSyncedViewportRef.current;
    if (!options?.force && last && last.x === v.x && last.y === v.y && last.zoom === v.zoom) {
      return;
    }
    lastSyncedViewportRef.current = { x: v.x, y: v.y, zoom: v.zoom };
    const config = playground.config as unknown as FlowGramPlaygroundViewportConfig;
    config.updateConfig?.({ scrollX: v.x, scrollY: v.y, zoom: v.zoom });
  }, [playground]);

  useEffect(() => {
    const config = playground.config as typeof playground.config & { readonly?: boolean };
    const previous = config.readonly;
    config.readonly = Boolean(props.readonly);
    return () => {
      config.readonly = previous;
    };
  }, [playground.config, props.readonly]);

  useEffect(() => {
    if (draggingRef.current) {
      return;
    }
    const nextSignature = workflowSignature(normalizeWorkflow(renderedWorkflow));
    const currentDocSignature = workflowSignature(normalizeWorkflow(doc.toJSON() as WorkflowJSON));
    if (lastWorkflowSignatureRef.current === nextSignature && currentDocSignature === nextSignature) {
      return;
    }
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(cloneWorkflow(renderedWorkflow))).finally(() => {
      lastWorkflowSignatureRef.current = nextSignature;
      reloadingRef.current = false;
      pushViewportToPlaygroundConfig(latestSchemaRef.current.editor.viewport, { force: true });
    });
  }, [doc, renderedWorkflow, pushViewportToPlaygroundConfig]);

  useEffect(() => {
    const disposable = dragService.onNodesDrag(event => {
      if (propsRef.current.readonly) {
        return;
      }
      if (event.type === "onDragStart") {
        draggingRef.current = true;
        const nativeEvent = (event as unknown as { originalEvent?: MouseEvent }).originalEvent;
        if (nativeEvent) {
          dragStartPosRef.current = { x: nativeEvent.clientX, y: nativeEvent.clientY };
        }
        return;
      }
      if (event.type === "onDragEnd") {
        draggingRef.current = false;
        const selectedIds = (selectService.selection ?? [])
          .map(selection => selection?.id)
          .filter((id): id is string => typeof id === "string" && id.length > 0);
        commitWorkflow(doc.toJSON() as WorkflowJSON, "flowgramNodeMove", {
          snapToGrid: propsRef.current.schema.editor.gridEnabled !== false,
          preferredNodeIds: selectedIds,
        });
      }
    });
    return () => disposable.dispose();
  }, [doc, dragService, selectService]);

  useEffect(() => {
    const disposable = doc.onContentChange(() => {
      if (reloadingRef.current || draggingRef.current || propsRef.current.readonly) {
        return;
      }
      const workflow = normalizeWorkflow(doc.toJSON() as WorkflowJSON);
      const invalidEdge = findInvalidBusinessEdge(workflow);
      if (invalidEdge) {
        Toast.warning("The selected ports cannot be connected.");
        disposeTemporaryEdgeLine(invalidEdge);
        return;
      }
      const previousWorkflow = latestSchemaRef.current.workflow;
      const edgesChanged = edgeStructureSignature(workflow) !== edgeStructureSignature(previousWorkflow);
      const nodesChanged = nodeStructureSignature(workflow) !== nodeStructureSignature(previousWorkflow);
      if (edgesChanged && !nodesChanged) {
        commitEdgeStructure(workflow, "flowgramEdgeStructureChange");
        return;
      }
      if (nodesChanged) {
        commitWorkflow(workflow, "flowgramWorkflowChange");
      }
    });
    return () => disposable.dispose();
  }, [doc, linesManager]);

  useEffect(() => {
    const disposable = selectService.onSelectionChanged(() => {
      const ids = (selectService.selection ?? [])
        .map(selection => selection?.id)
        .filter((id): id is string => typeof id === "string" && id.length > 0);
      propsRef.current.onSelectionChange(selectionFromIds(latestSchemaRef.current.workflow, ids));
    });
    return () => disposable.dispose();
  }, [selectService]);

  useEffect(() => {
    if (userViewportPanningRef.current) {
      return;
    }
    const v = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    pushViewportToPlaygroundConfig(v);
  }, [props.schema.editor.viewport?.x, props.schema.editor.viewport?.y, props.schema.editor.viewport?.zoom, pushViewportToPlaygroundConfig]);

  useEffect(() => {
    const typingSelector =
      "input, textarea, select, [contenteditable='true'], .semi-input-wrapper, .semi-textarea-wrapper, .cm-content, .cm-editor";
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.code !== "Space" && e.key !== " ") {
        return;
      }
      const t = e.target;
      if (t instanceof Element && t.closest(typingSelector)) {
        return;
      }
      e.preventDefault();
      setSpacePressed(true);
    };
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.code !== "Space" && e.key !== " ") {
        return;
      }
      setSpacePressed(false);
    };
    const onWindowBlur = () => {
      setSpacePressed(false);
    };
    window.addEventListener("keydown", onKeyDown, true);
    window.addEventListener("keyup", onKeyUp, true);
    window.addEventListener("blur", onWindowBlur);

    const onFitView = () => {
      fitViewportToWorkflow();
    };
    const onCenterView = () => {
      centerViewportToWorkflow();
    };
    const onZoom = (e: Event) => {
      const detail = (e as CustomEvent<{ delta?: number; zoom?: number }>).detail ?? {};
      if (detail.zoom !== undefined) {
        applyViewportZoomFromCanvasCenter(detail.zoom);
      } else if (detail.delta !== undefined) {
        const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
        applyViewportZoomFromCanvasCenter(Math.max(0.2, Math.min(2, v.zoom * (1 + detail.delta))));
      }
    };

    window.addEventListener("atlas:microflow-flowgram-fit-view", onFitView);
    window.addEventListener("atlas:microflow-flowgram-center-view", onCenterView);
    window.addEventListener("atlas:microflow-flowgram-zoom", onZoom);

    return () => {
      window.removeEventListener("keydown", onKeyDown, true);
      window.removeEventListener("keyup", onKeyUp, true);
      window.removeEventListener("blur", onWindowBlur);
      window.removeEventListener("atlas:microflow-flowgram-fit-view", onFitView);
      window.removeEventListener("atlas:microflow-flowgram-center-view", onCenterView);
      window.removeEventListener("atlas:microflow-flowgram-zoom", onZoom);
    };
  }, []);

  const dropPlacementFromClient = (
    clientX: number,
    clientY: number,
    item: MicroflowNodeRegistryItem,
    nativeEvent?: globalThis.MouseEvent,
  ): { position: MicroflowPoint; parentLoopObjectId?: string; valid: boolean; insertFlowId?: string } => {
    const rect = containerRef.current?.getBoundingClientRect();
    const viewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const fallback = clientPointToFlowGramPoint({ x: clientX, y: clientY }, rect, viewport);
    const dragPosition = nativeEvent
      ? playground.config.getPosFromMouseEvent?.(nativeEvent) as MicroflowPoint | undefined
      : undefined;
    const logicalCursor = dragPosition ?? fallback;
    const objectKind = objectKindFromRegistryItem(item);
    const hintedInsertFlowId = nativeEvent?.target instanceof Element
      ? selectionFromTarget(nativeEvent.target as HTMLElement, latestSchemaRef.current.workflow)?.flowId
      : undefined;
    const insertFlowId = hintedInsertFlowId
      ?? findNearestDropInsertFlowId(latestSchemaRef.current, logicalCursor, 24);
    const parentLoopObjectId = findLoopParentAtPoint(
      normalizeWorkflow(latestSchemaRef.current.workflow),
      logicalCursor,
    );
    const offset = getMendixMicroflowDropOffset(objectKind);
    const centered = {
      x: logicalCursor.x - offset.x,
      y: logicalCursor.y - offset.y,
    };
    return {
      position: gridEnabled ? snapMicroflowPoint(centered) : centered,
      parentLoopObjectId,
      valid: canDropRegistryObjectKindInLoop(objectKind, parentLoopObjectId),
      insertFlowId,
    };
  };

  const dropPlacementFromEvent = (event: DragEvent<HTMLDivElement>, item: MicroflowNodeRegistryItem) =>
    dropPlacementFromClient(event.clientX, event.clientY, item, event.nativeEvent);

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const payload = readMicroflowNodeDragPayload(event.dataTransfer);
    const item = payload ? microflowNodeRegistryByKey.get(payload.registryKey) : undefined;
    const placement = item ? dropPlacementFromEvent(event, item) : undefined;
    if (item && placement) {
      const insertAnchor = placement.insertFlowId
        ? resolveFlowInsertAnchor(latestSchemaRef.current, placement.insertFlowId)
        : undefined;
      setDropPreview({
        position: placement.position,
        size: getMendixMicroflowNodeSize(objectKindFromRegistryItem(item)),
        valid: placement.valid,
        insertFlowId: placement.insertFlowId,
        insertAnchor,
      });
    }
    event.dataTransfer.dropEffect = placement?.valid === false ? "none" : "copy";
    setDropActive(true);
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    setDropActive(false);
    setDropPreview(undefined);
    const payload = readMicroflowNodeDragPayload(event.dataTransfer);
    if (!payload) {
      Toast.warning("无法读取拖拽的微流节点。");
      return;
    }
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item) {
      Toast.warning("未找到对应的微流节点定义。");
      return;
    }
    if (!canDragRegistryItem(item)) {
      Toast.warning(getDisabledDragReason(item) ?? "This node cannot be added to Microflow.");
      return;
    }
    if (!canCreateRegistryItem(item, { microflowId: props.schema.id, schemaLoaded: true, readonly: props.readonly })) {
      Toast.warning("该节点当前不可拖拽创建。");
      return;
    }
    const placement = dropPlacementFromEvent(event, item);
    if (!placement.valid) {
      Toast.warning("该节点不能放置在当前 Loop 区域。");
      return;
    }
    props.onDropRegistryItem?.(item, placement.position, payload, {
      parentLoopObjectId: placement.parentLoopObjectId,
      insertFlowId: placement.insertFlowId,
    });
  };

  const handlePointerFallbackDrop = (event: MouseEvent<HTMLDivElement>) => {
    if (viewportPanPointerIdRef.current !== null) {
      return;
    }
    if (props.readonly) {
      return;
    }
    const payload = takeMicroflowNodePointerDrag();
    if (!payload) {
      return;
    }
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item) {
      Toast.warning("未找到对应的微流节点定义。");
      return;
    }
    if (!canDragRegistryItem(item)) {
      Toast.warning(getDisabledDragReason(item) ?? "This node cannot be added to Microflow.");
      return;
    }
    if (!canCreateRegistryItem(item, { microflowId: props.schema.id, schemaLoaded: true, readonly: props.readonly })) {
      Toast.warning("该节点当前不可拖拽创建。");
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const placement = dropPlacementFromClient(event.clientX, event.clientY, item, event.nativeEvent);
    setDropPreview(undefined);
    if (!placement.valid) {
      Toast.warning("该节点不能放置在当前 Loop 区域。");
      return;
    }
    props.onDropRegistryItem?.(item, placement.position, payload, {
      parentLoopObjectId: placement.parentLoopObjectId,
      insertFlowId: placement.insertFlowId,
    });
  };

  useEffect(() => {
    const sel = props.schema.editor.selection;
    const objectId = sel.objectId;
    const hasMulti = (sel.objectIds?.length ?? 0) > 1 || (sel.flowIds?.length ?? 0) > 1;
    if (!objectId || hasMulti) {
      setCanvasNodeToolbar(undefined);
      return;
    }
    const nodeEl = containerRef.current?.querySelector<HTMLElement>(`[data-microflow-object-id="${objectId}"]`);
    if (!nodeEl || !containerRef.current) {
      setCanvasNodeToolbar(undefined);
      return;
    }
    const containerRect = containerRef.current.getBoundingClientRect();
    const nodeRect = nodeEl.getBoundingClientRect();
    setCanvasNodeToolbar({
      x: nodeRect.left - containerRect.left + nodeRect.width / 2 - 44,
      y: nodeRect.bottom - containerRect.top + 8,
      objectId,
    });
  }, [props.schema.editor.selection]);

  const handleEdgeReconnectHandlePointerDown = (
    flowId: string,
    dragEndpoint: ReconnectFlowDragEndpoint,
    event: PointerEvent<HTMLButtonElement>,
  ) => {
    if (props.readonly) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const geometry = reconnectGeometry.edgeByFlowId.get(flowId);
    if (!geometry || geometry.edgeKind === "annotation") {
      return;
    }
    const pointer = clientPointToFlowGramPoint(
      { x: event.clientX, y: event.clientY },
      containerRef.current?.getBoundingClientRect(),
      latestSchemaRef.current.editor.viewport ?? { x: 0, y: 0, zoom: 1 },
    );
    const fixed = dragEndpoint === "source"
      ? {
          point: geometry.targetPoint,
          portId: geometry.targetPortId,
          objectId: geometry.targetObjectId,
        }
      : {
          point: geometry.sourcePoint,
          portId: geometry.sourcePortId,
          objectId: geometry.sourceObjectId,
        };
    const original = dragEndpoint === "source"
      ? {
          point: geometry.sourcePoint,
          portId: geometry.sourcePortId,
          objectId: geometry.sourceObjectId,
        }
      : {
          point: geometry.targetPoint,
          portId: geometry.targetPortId,
          objectId: geometry.targetObjectId,
        };
    edgeHoverCandidateRef.current = flowId;
    clearEdgeHoverTimer();
    setHoveredFlowId(flowId);
    setReconnectState({
      flowId,
      dragEndpoint,
      pointer,
      fixedPoint: fixed.point,
      fixedPortId: fixed.portId,
      fixedObjectId: fixed.objectId,
      originalPoint: original.point,
      originalPortId: original.portId,
      originalObjectId: original.objectId,
    });
  };

  const clearEdgeHoverTimer = () => {
    if (edgeHoverTimerRef.current !== undefined) {
      window.clearTimeout(edgeHoverTimerRef.current);
      edgeHoverTimerRef.current = undefined;
    }
  };

  const handleMouseMoveCapture = (event: MouseEvent<HTMLDivElement>) => {
    if (reconnectState) {
      return;
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    const flowId = target?.closest<HTMLElement>("[data-flow-id]")?.dataset.flowId;
    if (flowId) {
      if (hoveredFlowId === flowId || edgeHoverCandidateRef.current === flowId) {
        return;
      }
      clearEdgeHoverTimer();
      edgeHoverCandidateRef.current = flowId;
      edgeHoverTimerRef.current = window.setTimeout(() => {
        setHoveredFlowId(current => current === flowId ? current : flowId);
        edgeHoverTimerRef.current = undefined;
      }, 200);
      return;
    }
    edgeHoverCandidateRef.current = undefined;
    clearEdgeHoverTimer();
    setHoveredFlowId(undefined);
  };

  const openContextMenuFromTarget = (target: HTMLElement | undefined, point: { x: number; y: number }): boolean => {
    const selection = selectionFromTarget(target, latestSchemaRef.current.workflow);
    if (!selection) {
      return false;
    }
    props.onSelectionChange(selection);
    props.onNodeContextMenu?.(selection, point);
    return true;
  };

  const handleClickCapture = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button !== 0) {
      return;
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    if (target?.closest(".microflow-flowgram-reconnect-handle")) {
      return;
    }
    // 点击画布 UI 控件（工具栏、多选栏等）时不干预 selection 状态
    if (target?.closest('[data-flow-editor-selectable="false"], .microflow-flowgram-canvas-controls')) {
      return;
    }
    const selectionFromEventTarget = selectionFromTarget(target, latestSchemaRef.current.workflow);
    const fallbackFlowId = selectionFromEventTarget
      ? undefined
      : findNearestDropInsertFlowId(
        latestSchemaRef.current,
        clientPointToFlowGramPoint(
          { x: event.clientX, y: event.clientY },
          containerRef.current?.getBoundingClientRect(),
          latestSchemaRef.current.editor.viewport ?? { x: 0, y: 0, zoom: 1 },
        ),
        10,
      );
    const selection = selectionFromEventTarget ?? (fallbackFlowId
      ? {
          objectId: undefined,
          flowId: fallbackFlowId,
          collectionId: undefined,
          objectIds: [],
          flowIds: [fallbackFlowId],
          mode: "single" as const,
        }
      : undefined);
    if (selection) {
      props.onSelectionChange(selection);
      // 确保 canvas 容器获得焦点，否则 Delete 等键盘快捷键的 container.contains(event.target) 检查会失败
      if (document.activeElement !== containerRef.current && !containerRef.current?.contains(document.activeElement)) {
        containerRef.current?.focus({ preventScroll: true });
      }
      if (dragStartPosRef.current) {
        const dx = event.clientX - dragStartPosRef.current.x;
        const dy = event.clientY - dragStartPosRef.current.y;
        const distance = Math.sqrt(dx * dx + dy * dy);
        dragStartPosRef.current = null;
        if (distance > 3) {
          return;
        }
      }
      dragStartPosRef.current = null;
      props.onNodeClickChange?.(selection);
    } else {
      // 点击空白画布时清除选中状态（连线高亮消失）
      const currentSelection = latestSchemaRef.current.editor?.selection;
      if (currentSelection?.flowId || currentSelection?.objectId) {
        props.onSelectionChange({ objectId: undefined, flowId: undefined, collectionId: undefined, objectIds: [], flowIds: [], mode: "none" });
      }
    }
  };

  const handleDoubleClick = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button !== 0) {
      return;
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    const selection = selectionFromTarget(target, latestSchemaRef.current.workflow);
    if (selection && selection.objectId) {
      props.onNodeDoubleClick?.(selection);
    } else {
      props.onCanvasBlankDoubleClick?.({ x: event.clientX, y: event.clientY });
    }
  };

  const handleMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button === 1) {
      const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
      if (!isPointerTargetPanExempt(target, spacePressedRef.current)) {
        event.preventDefault();
      }
      return;
    }
    if (event.button !== 2) {
      return;
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    const point = { x: event.clientX, y: event.clientY };
    if (openContextMenuFromTarget(target, point)) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  const handleContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    if (suppressNextContextMenuRef.current) {
      suppressNextContextMenuRef.current = false;
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    const point = { x: event.clientX, y: event.clientY };
    if (openContextMenuFromTarget(target, point)) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    props.onCanvasBlankContextMenu?.(point);
    if (!isPointerTargetPanExempt(target, spacePressedRef.current)) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (reconnectState) {
      const pointer = clientPointToFlowGramPoint(
        { x: event.clientX, y: event.clientY },
        containerRef.current?.getBoundingClientRect(),
        latestSchemaRef.current.editor.viewport ?? { x: 0, y: 0, zoom: 1 },
      );
      const candidateDirection = reconnectState.dragEndpoint === "source" ? "output" : "input";
      const candidatePort = nearestReconnectPortCandidate(reconnectGeometry.ports, pointer, candidateDirection, 48);
      const nextCandidate = candidatePort
        ? (() => {
            const check = propsRef.current.onEvaluateFlowReconnect?.({
              flowId: reconnectState.flowId,
              dragEndpoint: reconnectState.dragEndpoint,
              targetObjectId: candidatePort.objectId,
              targetPortId: candidatePort.id,
            }) ?? { allowed: true };
            return {
              objectId: candidatePort.objectId,
              portId: candidatePort.id,
              point: candidatePort.point,
              allowed: check.allowed,
              message: check.message,
            };
          })()
        : undefined;
      setReconnectState(current => current ? { ...current, pointer, candidate: nextCandidate, failedAt: undefined } : current);
      return;
    }
    if (viewportPanPointerIdRef.current !== event.pointerId || !viewportPanOriginRef.current) {
      return;
    }
    if (draggingRef.current) {
      return;
    }
    event.preventDefault();
    const orig = viewportPanOriginRef.current;
    const dx = event.clientX - orig.clientX;
    const dy = event.clientY - orig.clientY;
    if (Math.abs(dx) + Math.abs(dy) > 2) {
      viewportPanMovedRef.current = true;
    }
    const next = {
      x: orig.viewportX - dx,
      y: orig.viewportY - dy,
      zoom: orig.zoom,
    };
    propsRef.current.onViewportChange?.(next);
    const config = playground.config as unknown as FlowGramPlaygroundViewportConfig;
    config.updateConfig?.({ scrollX: next.x, scrollY: next.y, zoom: next.zoom });
    lastSyncedViewportRef.current = { x: next.x, y: next.y, zoom: next.zoom };
  };

  const endViewportPointerPan = (event: PointerEvent<HTMLDivElement>) => {
    if (reconnectState) {
      const candidate = reconnectState.candidate;
      if (candidate?.allowed) {
        const result = propsRef.current.onReconnectFlow?.({
          flowId: reconnectState.flowId,
          dragEndpoint: reconnectState.dragEndpoint,
          targetObjectId: candidate.objectId,
          targetPortId: candidate.portId,
        }) ?? { ok: true };
        if (result.ok) {
          setPortFlashState({ portId: candidate.portId, tick: Date.now() });
          setReconnectState(undefined);
          window.setTimeout(() => setPortFlashState(undefined), 350);
          return;
        }
      }
      Toast.warning({ content: candidate?.message || "无法连接", duration: 1.5 });
      setReconnectState(current => current ? { ...current, failedAt: Date.now() } : current);
      window.setTimeout(() => setReconnectState(undefined), 300);
      return;
    }
    if (viewportPanPointerIdRef.current !== event.pointerId) {
      return;
    }
    const moved = viewportPanMovedRef.current;
    const button = viewportPanButtonRef.current;
    viewportPanPointerIdRef.current = null;
    viewportPanOriginRef.current = null;
    viewportPanButtonRef.current = null;
    userViewportPanningRef.current = false;
    setIsViewportPanGrabbing(false);
    selectorBoxConfig.disabled = panToolActiveRef.current || spacePressedRef.current;
    try {
      event.currentTarget.releasePointerCapture(event.pointerId);
    } catch {
      // Ignore — capture may already be released.
    }
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    const intent = resolveViewportPanEndIntent({
      button,
      moved,
      target,
      spacePressed: spacePressedRef.current,
    });
    if (intent === "blank-context-menu") {
      propsRef.current.onCanvasBlankContextMenu?.({ x: event.clientX, y: event.clientY });
    } else if (intent === "blank-click") {
      propsRef.current.onCanvasBlankClick?.({ x: event.clientX, y: event.clientY });
    }
  };

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target instanceof Element ? event.target as HTMLElement : undefined;
    if (target?.closest(".microflow-flowgram-reconnect-handle")) {
      return;
    }
    const shouldPan = shouldViewportPanFromPointerDown({
      target,
      button: event.button,
      panToolActive: panToolActiveRef.current,
      spacePressed: spacePressedRef.current,
      draggingNode: draggingRef.current,
    });
    if (shouldPan) {
      selectorBoxConfig.disabled = true;
      event.preventDefault();
      event.stopPropagation();
      const ne = event.nativeEvent;
      if (typeof ne.stopImmediatePropagation === "function") {
        ne.stopImmediatePropagation();
      }
      viewportPanMovedRef.current = false;
      suppressNextContextMenuRef.current = event.button === 2;
      viewportPanButtonRef.current = event.button;
      viewportPanPointerIdRef.current = event.pointerId;
      userViewportPanningRef.current = true;
      setIsViewportPanGrabbing(true);
      const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
      viewportPanOriginRef.current = {
        clientX: event.clientX,
        clientY: event.clientY,
        viewportX: v.x,
        viewportY: v.y,
        zoom: v.zoom,
      };
      try {
        event.currentTarget.setPointerCapture(event.pointerId);
      } catch {
        // Capture not supported or target detached.
      }
      return;
    }
    if (isPointerTargetPanExempt(target, spacePressedRef.current)) {
      return;
    }
    propsRef.current.onCanvasBlankClick?.({ x: event.clientX, y: event.clientY });
  };

  const handleLostPointerCapture = (event: PointerEvent<HTMLDivElement>) => {
    if (viewportPanPointerIdRef.current === event.pointerId) {
      viewportPanPointerIdRef.current = null;
      viewportPanOriginRef.current = null;
      viewportPanButtonRef.current = null;
      userViewportPanningRef.current = false;
      setIsViewportPanGrabbing(false);
      selectorBoxConfig.disabled = panToolActiveRef.current || spacePressedRef.current;
    }
  };

  const focusNode = (objectId: string) => {
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(objectId);
    if (node) {
      void selectService.selectNodeAndScrollToView(node, true);
    }
    props.onSelectionChange({ objectId, flowId: undefined, collectionId: MICROFLOW_ROOT_COLLECTION_ID });
  };

  const fitViewportToWorkflow = () => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) {
      props.onViewportChange?.({ x: 0, y: 0, zoom: 1 });
      return;
    }
    props.onViewportChange?.(fitViewportForWorkflow(props.schema.workflow, rect));
  };

  const centerViewportToWorkflow = () => {
    const rect = containerRef.current?.getBoundingClientRect();
    const viewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    if (!rect) {
      props.onViewportChange?.({ x: 0, y: 0, zoom: viewport.zoom });
      return;
    }
    props.onViewportChange?.(centerViewportForWorkflow(props.schema.workflow, rect, viewport.zoom));
  };

  useEffect(() => {
    if (initialViewportFitDoneRef.current) {
      return;
    }
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect || !props.schema.workflow.nodes.length) {
      return;
    }
    initialViewportFitDoneRef.current = true;
    const nodeCount = props.schema.workflow.nodes.length;
    const savedViewport = props.schema.editor?.viewport;
    const hasNonDefaultViewport = savedViewport && (
      Math.abs(savedViewport.x) > 20 ||
      Math.abs(savedViewport.y) > 20 ||
      Math.abs(savedViewport.zoom - 1) > 0.05
    );
    if (nodeCount > 4 && !hasNonDefaultViewport) {
      // Fit all nodes into view for larger flows without a saved viewport position
      props.onViewportChange?.(fitViewportForWorkflow(props.schema.workflow, rect), { skipDirty: true });
    } else {
      props.onViewportChange?.(initialViewportFromStartNode(props.schema.workflow, rect), { skipDirty: true });
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.onViewportChange]);

  useEffect(() => {
    if (props.focusObjectId) {
      focusNode(props.focusObjectId);
    }
  }, [props.focusObjectId, props.focusRequestKey]);

  useEffect(() => {
    const root = containerRef.current;
    if (!root) {
      return;
    }
    const onWheel = (e: WheelEvent) => {
      const t = e.target;
      if (!(t instanceof Element) || !root.contains(t)) {
        return;
      }

      if (isPointerTargetPanExempt(t as HTMLElement, false)) {
        return;
      }

      // 普通滚轮缩放；Ctrl/Meta+滚轮使用更小倍率，适合微调。
      if (!panToolActiveRef.current || e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
        const rect = root.getBoundingClientRect();
        const scale = Math.exp(-e.deltaY * ((e.ctrlKey || e.metaKey) ? 0.0016 : 0.0032));
        const localX = e.clientX - rect.left;
        const localY = e.clientY - rect.top;
        const next = microflowZoomViewportAtLocalPoint(v, localX, localY, v.zoom * scale);
        if (Math.abs(next.zoom - v.zoom) < 1e-6 && Math.abs(next.x - v.x) < 1e-6 && Math.abs(next.y - v.y) < 1e-6) {
          return;
        }
        const cfg = playgroundRef.current.config as unknown as FlowGramPlaygroundViewportConfig;
        if (typeof cfg.zoom === "function") {
          cfg.zoom(next.zoom);
        }
        cfg.updateConfig?.({ zoom: next.zoom, scrollX: next.x, scrollY: next.y });
        lastSyncedViewportRef.current = next;
        propsRef.current.onViewportChange?.(next);
        emitCanvasZoomChange(next.zoom);
        return;
      }

      // 平移工具模式下保留滚轮平移/缩放体验。
      e.preventDefault();
      e.stopPropagation();
      const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
      const rect = root.getBoundingClientRect();
      const localX = e.clientX - rect.left;
      const localY = e.clientY - rect.top;
      const next = zoomViewportForPanToolWheel(v, localX, localY, e.deltaY);
      if (Math.abs(next.zoom - v.zoom) < 1e-6 && Math.abs(next.x - v.x) < 1e-6 && Math.abs(next.y - v.y) < 1e-6) {
        return;
      }
      const cfg = playgroundRef.current.config as unknown as FlowGramPlaygroundViewportConfig;
      if (typeof cfg.zoom === "function") {
        cfg.zoom(next.zoom);
      }
      cfg.updateConfig?.({ zoom: next.zoom, scrollX: next.x, scrollY: next.y });
      lastSyncedViewportRef.current = next;
      propsRef.current.onViewportChange?.(next);
      emitCanvasZoomChange(next.zoom);
    };
    root.addEventListener("wheel", onWheel, { passive: false, capture: true });
    return () => root.removeEventListener("wheel", onWheel, { capture: true });
  }, []);

  useEffect(() => {
    emitCanvasZoomChange(props.schema.editor.viewport?.zoom ?? 1);
  }, [emitCanvasZoomChange, props.schema.editor.viewport?.zoom]);

  useEffect(() => {
    const canvasAPI = {
      zoomIn: () => {
        const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
        applyViewportZoomFromCanvasCenter(v.zoom + 0.1);
      },
      zoomOut: () => {
        const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
        applyViewportZoomFromCanvasCenter(Math.max(0.1, v.zoom - 0.1));
      },
      fitView: () => {
        fitViewportToWorkflow();
      },
      resetZoom: () => {
        applyViewportZoomFromCanvasCenter(1);
      },
      autoLayout: () => {
        propsRef.current.onAutoLayout?.();
      },
      undo: () => {
        propsRef.current.onUndo?.();
      },
      redo: () => {
        propsRef.current.onRedo?.();
      },
    };
    (window as any).__canvasAPI = canvasAPI;
    return () => {
      if ((window as any).__canvasAPI === canvasAPI) {
        delete (window as any).__canvasAPI;
      }
    };
  }, [applyViewportZoomFromCanvasCenter, fitViewportToWorkflow, props.onAutoLayout, props.onRedo, props.onUndo]);

  const currentViewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };

  const reconnectOverlay = reconnectState ? (() => {
    const fixedPx = logicalToContainer(reconnectState.fixedPoint, currentViewport);
    const dragPx = logicalToContainer(reconnectState.candidate?.point ?? reconnectState.pointer, currentViewport);
    const originPx = logicalToContainer(reconnectState.originalPoint, currentViewport);
    return (
      <>
        <svg className="microflow-flowgram-reconnect-overlay" aria-hidden="true">
          <line
            x1={fixedPx.x}
            y1={fixedPx.y}
            x2={dragPx.x}
            y2={dragPx.y}
            className={reconnectState.candidate?.allowed === false ? "is-invalid" : ""}
          />
        </svg>
        <div
          className="microflow-flowgram-reconnect-origin-mark"
          style={{ left: originPx.x - 6, top: originPx.y - 6 }}
          aria-hidden="true"
        >
          ×
        </div>
      </>
    );
  })() : null;

  return (
    <div
      ref={containerRef}
      data-testid="microflow-flowgram-canvas"
      tabIndex={-1}
      className={`microflow-flowgram-canvas${dropActive ? " is-drop-active" : ""}${gridEnabled ? "" : " is-grid-hidden"}${panToolActive || spacePressed ? " is-pan-cursor" : ""}${isViewportPanGrabbing ? " is-pan-grabbing" : ""}${reconnectState ? " is-reconnect-dragging" : ""}`}
      onDragEnterCapture={event => {
        if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
          return;
        }
        event.preventDefault();
        setDropActive(true);
      }}
      onDragOverCapture={handleDragOver}
      onDragLeaveCapture={event => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setDropActive(false);
          setDropPreview(undefined);
        }
      }}
      onDropCapture={handleDrop}
      onMouseMoveCapture={handleMouseMoveCapture}
      onMouseLeave={() => {
        edgeHoverCandidateRef.current = undefined;
        clearEdgeHoverTimer();
        if (!reconnectState) {
          setHoveredFlowId(undefined);
        }
      }}
      onAuxClick={event => {
        if (event.button === 1) {
          event.preventDefault();
        }
      }}
      onClickCapture={handleClickCapture}
      onDoubleClickCapture={handleDoubleClick}
      onMouseDownCapture={handleMouseDown}
      onMouseUpCapture={handlePointerFallbackDrop}
      onContextMenuCapture={handleContextMenu}
      onPointerDownCapture={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUpCapture={endViewportPointerPan}
      onPointerCancelCapture={endViewportPointerPan}
      onLostPointerCapture={handleLostPointerCapture}
    >
      <PlaygroundReactRenderer />
      {hoveredEdgeGeometry && !props.readonly && !reconnectState ? (
        <>
          <button
            type="button"
            className="microflow-flowgram-reconnect-handle microflow-flowgram-reconnect-handle--source"
            style={(() => {
              const c = logicalToContainer(hoveredEdgeGeometry.sourcePoint, currentViewport);
              return { left: c.x - 5, top: c.y - 5 };
            })()}
            onPointerDown={event => handleEdgeReconnectHandlePointerDown(hoveredEdgeGeometry.flowId, "source", event)}
            aria-label="Reconnect source endpoint"
          />
          <button
            type="button"
            className="microflow-flowgram-reconnect-handle microflow-flowgram-reconnect-handle--target"
            style={(() => {
              const c = logicalToContainer(hoveredEdgeGeometry.targetPoint, currentViewport);
              return { left: c.x - 5, top: c.y - 5 };
            })()}
            onPointerDown={event => handleEdgeReconnectHandlePointerDown(hoveredEdgeGeometry.flowId, "target", event)}
            aria-label="Reconnect target endpoint"
          />
        </>
      ) : null}
      {reconnectOverlay}
      {dropPreview ? (
        <div
          className={`microflow-flowgram-drop-preview${dropPreview.valid ? "" : " is-invalid"}`}
          style={{
            left: dropPreview.position.x - dropPreview.size.width / 2,
            top: dropPreview.position.y - dropPreview.size.height / 2,
            width: dropPreview.size.width,
            height: dropPreview.size.height,
          }}
        />
      ) : null}
      {dropPreview?.insertAnchor ? (
        <div
          className="microflow-flowgram-drop-insert-anchor"
          style={{
            left: dropPreview.insertAnchor.x - 7,
            top: dropPreview.insertAnchor.y - 7,
          }}
        />
      ) : null}
      {showBuiltInToolbar ? (
        <div className="microflow-flowgram-canvas-controls" onClickCapture={e => e.stopPropagation()}>
          <FlowGramMicroflowToolbar
            microflowComplexity={microflowComplexity}
            canUndo={props.canUndo}
            canRedo={props.canRedo}
            onUndo={props.onUndo}
            onRedo={props.onRedo}
            onAutoLayout={props.onAutoLayout}
            readonly={props.readonly}
            viewport={props.schema.editor.viewport}
            onViewportChange={viewport => props.onViewportChange?.(viewport)}
            onFitView={fitViewportToWorkflow}
            onCenterView={centerViewportToWorkflow}
            gridEnabled={gridEnabled}
            onToggleGrid={() => props.onToggleGrid?.(!gridEnabled)}
            miniMapVisible={miniMapVisible}
            onToggleMiniMap={() => props.onToggleMiniMap?.(!miniMapVisible)}
            panToolActive={panToolActive}
            onTogglePanTool={togglePanTool}
            applyZoomFromCanvasCenter={applyViewportZoomFromCanvasCenter}
            dirty={props.dirty}
            saving={props.saving}
            validating={props.validating}
            validationIssues={props.validationIssues}
            onOpenProblemsPanel={props.onOpenProblemsPanel}
            onRun={props.onRun}
            onStopRun={props.onStopRun}
            isRunning={props.isRunning}
            onNavigateToIssue={props.onNavigateToIssue ?? ((objectId) => focusNode(objectId))}
          />
        </div>
      ) : null}
      {miniMapVisible ? <FlowGramMicroflowNativeMiniMap schema={props.schema} onFocusNode={focusNode} /> : null}
      {(props.schema.workflow.nodes ?? []).length === 0 && !props.readonly ? (
        <div
          style={{
            position: "absolute",
            inset: 0,
            display: "grid",
            placeItems: "center",
            pointerEvents: "none",
            zIndex: 5,
          }}
        >
          <div style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 12,
            padding: "24px 32px",
            borderRadius: 12,
            border: "1px dashed rgba(255,255,255,0.12)",
            background: "rgba(255,255,255,0.03)",
            maxWidth: 280,
            textAlign: "center",
          }}>
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none" aria-hidden="true">
              <rect x="6" y="14" width="16" height="20" rx="3" fill="rgba(255,255,255,0.06)" stroke="rgba(255,255,255,0.2)" strokeWidth="1.5" strokeDasharray="4 2" />
              <path d="M30 24l12 0M36 18l6 6-6 6" stroke="#4a9eff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              <circle cx="38" cy="24" r="6" fill="rgba(74,158,255,0.12)" stroke="rgba(74,158,255,0.6)" strokeWidth="1.2" />
            </svg>
            <div style={{ color: "rgba(255,255,255,0.55)", fontSize: 14, lineHeight: "20px" }}>
              从左侧节点面板拖拽节点到画布开始设计微流
            </div>
            <div style={{ color: "rgba(255,255,255,0.3)", fontSize: 12 }}>
              或双击画布快速添加节点
            </div>
          </div>
        </div>
      ) : null}
      {canvasNodeToolbar && !props.readonly ? (
        <FlowGramNodeToolbar
          x={canvasNodeToolbar.x}
          y={canvasNodeToolbar.y}
          nodeId={canvasNodeToolbar.objectId}
          onQuickAdd={props.onNodeToolbarQuickAdd ? () => props.onNodeToolbarQuickAdd!(canvasNodeToolbar.objectId, {
            x: canvasNodeToolbar.x,
            y: canvasNodeToolbar.y,
          }) : undefined}
          onQuickConnect={props.onNodeToolbarQuickConnect
            ? (item) => props.onNodeToolbarQuickConnect!(canvasNodeToolbar.objectId, item)
            : undefined
          }
          onDuplicate={() => props.onDuplicateSelection?.()}
        />
      ) : null}
      <MicroflowMultiSelectBar
        selection={props.schema.editor.selection}
        readonly={props.readonly}
        onDelete={props.onDeleteSelection}
        onClear={props.onClearSelection ?? (() => props.onCanvasBlankClick?.())}
      />
      <MicroflowNodeSpotlight
        workflow={props.schema.workflow as import("../schema/types").MicroflowWorkflowJSON}
        onNavigate={focusNode}
      />
    </div>
  );
}

type MultiSelectBarSelection = {
  mode?: string;
  objectIds?: string[];
  flowIds?: string[];
  objectId?: string;
  flowId?: string;
};

function MicroflowMultiSelectBar(props: {
  selection: MultiSelectBarSelection;
  readonly?: boolean;
  onDelete?: () => void;
  onClear?: () => void;
}) {
  const { selection, readonly, onDelete, onClear } = props;
  const objectCount = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter(Boolean))].length;
  const flowCount = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter(Boolean))].length;
  const total = objectCount + flowCount;
  /** 单节点有画布浮动工具栏；仅选中一条连线时与 Delete 键对齐，提供可见删除入口 */
  const showEdgeOnlySingleSelectBar = objectCount === 0 && flowCount === 1;
  if (total < 2 && !showEdgeOnlySingleSelectBar) {
    return null;
  }
  const label = objectCount > 0 && flowCount > 0
    ? `已选 ${objectCount} 个节点、${flowCount} 条连线`
    : objectCount > 0
      ? `已选 ${objectCount} 个节点`
      : `已选 ${flowCount} 条连线`;

  return (
    <div
      className="microflow-multiselect-bar"
      data-flow-editor-selectable="false"
      onMouseDown={e => e.stopPropagation()}
      onPointerDown={e => e.stopPropagation()}
    >
      <span className="microflow-multiselect-bar__label">{label}</span>
      <Space spacing={4}>
        {!readonly && onDelete ? (
          <Button
            size="small"
            type="danger"
            theme="light"
            icon={<IconDelete />}
            onClick={e => { e.stopPropagation(); onDelete(); }}
          >
            删除
          </Button>
        ) : null}
        <Button size="small" theme="borderless" onClick={e => { e.stopPropagation(); onClear?.(); }}>
          取消选择
        </Button>
      </Space>
    </div>
  );
}

export function FlowGramMicroflowNativeCanvas(props: FlowGramMicroflowNativeCanvasProps) {
  // 持久化 ref，跨 structureKey remount 保持 viewport 初始化标记，避免重连/拖入节点时视口抖动
  const viewportInitializedRef = useRef(false);
  const structureKey = workflowRenderStructureKey(props.schema.workflow);
  const edgeDataByLineKey = useMemo(
    () => createEdgeDataByLineKey(props.schema.workflow),
    [props.schema.workflow],
  );
  const usageHighlightState = useMemo(
    () => ({
      selectedObjectId: props.usageHighlights?.selectedObjectId,
      sourceNodeIds: props.usageHighlights?.sourceNodeIds ?? [],
      consumerNodeIds: props.usageHighlights?.consumerNodeIds ?? [],
    }),
    [props.usageHighlights],
  );
  const selectedFlowId = props.schema.editor.selection?.flowId;
  return (
    <MicroflowNodeViewModesContext.Provider value={props.nodeViewModes ?? {}}>
      <MicroflowNodeUsageHighlightsContext.Provider value={usageHighlightState}>
        <MicroflowEdgeDataContext.Provider value={edgeDataByLineKey}>
          <MicroflowSelectedFlowIdContext.Provider value={selectedFlowId}>
            <FlowGramMicroflowProvider key={structureKey}>
              <FlowGramMicroflowNativeCanvasInner {...props} viewportInitializedRef={viewportInitializedRef} />
            </FlowGramMicroflowProvider>
          </MicroflowSelectedFlowIdContext.Provider>
        </MicroflowEdgeDataContext.Provider>
      </MicroflowNodeUsageHighlightsContext.Provider>
    </MicroflowNodeViewModesContext.Provider>
  );
}
