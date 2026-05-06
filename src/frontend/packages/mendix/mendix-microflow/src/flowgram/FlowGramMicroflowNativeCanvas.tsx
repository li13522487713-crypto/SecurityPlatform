import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent, type MouseEvent, type PointerEvent } from "react";

import { Toast } from "@douyinfe/semi-ui";
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
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowMetadataCatalog } from "../metadata";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "../variables/variable-index";
import { getVariablesBeforeObject, type MicroflowVariableQueryOptions } from "../variables/variable-scope-engine";
import type { MicroflowSchema } from "../schema";
import { FlowGramMicroflowRuntimeContext } from "./FlowGramMicroflowContext";
import {
  canCreateRegistryItem,
  canDragRegistryItem,
  getDisabledDragReason,
  hasMicroflowNodeDragType,
  microflowNodeRegistryByKey,
  readMicroflowNodeDragPayload,
  takeMicroflowNodePointerDrag,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
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
  workflowEdgeById,
  workflowNodeById,
} from "./flowgram-native-schema";
import { FlowGramMicroflowProvider } from "./FlowGramMicroflowProvider";
import { FlowGramMicroflowStatusStrip } from "./FlowGramMicroflowStatusStrip";
import { FlowGramMicroflowToolbar } from "./FlowGramMicroflowToolbar";
import { flowGramPortsForObjectKind } from "./adapters/flowgram-port-factory";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData, FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";
import "@flowgram-adapter/free-layout-editor/css-load";
import "./styles/flowgram-microflow-canvas.css";
import "./styles/flowgram-microflow-port.css";
import "./styles/flowgram-microflow-line.css";

const MICROFLOW_GRID_SIZE = 16;

export interface FlowGramMicroflowNativeCanvasProps {
  schema: MicroflowDesignSchema;
  validationIssues: MicroflowValidationIssue[];
  runtimeTrace: MicroflowTraceFrame[];
  focusObjectId?: string;
  focusRequestKey?: number;
  readonly?: boolean;
  metadataCatalog?: MicroflowMetadataCatalog;
  expandedObjectId?: string | null;
  onExpandChange?: (objectId: string | null) => void;
  registerDraftValidator?: (fn: (() => { valid: boolean; summary: string }) | null) => void;
  onSchemaChange: (nextSchema: MicroflowDesignSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onCanvasBlankClick?: () => void;
  onNodeContextMenu?: (selection: FlowGramMicroflowSelection, point: { x: number; y: number }) => void;
  onDropRegistryItem?: (
    item: MicroflowNodeRegistryItem,
    position: MicroflowPoint,
    payload: MicroflowNodeDragPayload,
  ) => void;
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
  onOpenProblemsPanel?: () => void;
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

function edgeStructureSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  return JSON.stringify(
    ((workflow.edges ?? []) as Array<WorkflowEdgeJSON | MicroflowWorkflowEdgeJSON>)
      .map(edge => ({
        id: edgeId(edge),
        key: edgeKey(edge),
        data: (edge as MicroflowWorkflowEdgeJSON).data,
      }))
      .sort((a, b) => String(a.id ?? a.key).localeCompare(String(b.id ?? b.key))),
  );
}

function nodeStructureSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  return JSON.stringify(
    ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[])
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
    },
    meta: {
      ...node.meta,
      position: {
        x: Number(node.meta?.position?.x ?? 0),
        y: Number(node.meta?.position?.y ?? 0),
      },
      collectionId: node.meta?.collectionId ?? data?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID,
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
      validationState: data?.validationState ?? "valid",
      runtimeState: data?.runtimeState ?? "idle",
    },
  };
}

function normalizeWorkflow(workflow: WorkflowJSON | MicroflowWorkflowJSON, options: { snapToGrid?: boolean } = {}): WorkflowJSON {
  return {
    ...workflow,
    nodes: ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[]).map(node => {
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
    }) as WorkflowJSON["nodes"],
    edges: ((workflow.edges ?? []) as MicroflowWorkflowEdgeJSON[]).map(ensureEdgeData) as WorkflowJSON["edges"],
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
    x: Math.round(rect.width / 2 - (bounds.minX + bounds.width / 2) * zoom),
    y: Math.round(rect.height / 2 - (bounds.minY + bounds.height / 2) * zoom),
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
  const source = workflowNodeById(workflow, String(edge.sourceNodeID));
  const target = workflowNodeById(workflow, String(edge.targetNodeID));
  if (!source || !target || source.id === target.id) {
    return false;
  }
  const sourceKind = (source.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? source.type;
  const targetKind = (target.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? target.type;
  if (sourceKind === "endEvent" || targetKind === "startEvent") {
    return false;
  }
  const matchingEdges = ((workflow.edges ?? []) as WorkflowEdgeJSON[]).filter(candidate => edgeKey(candidate) === edgeKey(edge));
  if (matchingEdges.length > 1) {
    return false;
  }
  return true;
}

function findInvalidBusinessEdge(workflow: WorkflowJSON): WorkflowEdgeJSON | undefined {
  return ((workflow.edges ?? []) as WorkflowEdgeJSON[]).find(edge => !edgeIsBusinessValid(workflow, edge));
}

function selectionFromIds(workflow: MicroflowWorkflowJSON, ids: string[]): FlowGramMicroflowSelection {
  const objectIds = ids.filter(id => Boolean(workflowNodeById(workflow, id)));
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
          x={-viewport.x / Math.max(0.2, viewport.zoom)}
          y={-viewport.y / Math.max(0.2, viewport.zoom)}
          width={viewportWidth}
          height={viewportHeight}
          className="microflow-flowgram-minimap-viewport"
        />
      </svg>
    </div>
  );
}

function FlowGramMicroflowNativeCanvasInner(props: FlowGramMicroflowNativeCanvasProps) {
  const [localExpandedObjectId, setLocalExpandedObjectId] = useState<string | null>(null);
  const effectiveExpandedObjectId = props.expandedObjectId !== undefined ? props.expandedObjectId : localExpandedObjectId;
  const effectiveOnExpandChange = props.onExpandChange ?? setLocalExpandedObjectId;

  // MicroflowDesignSchema shares variable/parameter data with MicroflowAuthoringSchema;
  // cast for the variable engine which only uses those shared fields at runtime.
  const schemaAsAuthoring = props.schema as unknown as MicroflowSchema;

  const variableIndex = useMemo(
    () => buildVariableIndex(schemaAsAuthoring, props.metadataCatalog ?? EMPTY_MICROFLOW_METADATA_CATALOG),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [props.schema, props.metadataCatalog],
  );

  const runtimeTraceByObjectId = useMemo(
    () => new Map(props.runtimeTrace.map(f => [f.objectId, f])),
    [props.runtimeTrace],
  );

  const getVariablesForNode = useCallback(
    (objectId: string, options?: MicroflowVariableQueryOptions) =>
      getVariablesBeforeObject(schemaAsAuthoring, variableIndex, objectId, options),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [props.schema, variableIndex],
  );

  const playground = usePlayground();
  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const dragService = useService<WorkflowDragService>(WorkflowDragService);
  const linesManager = useService<WorkflowLinesManager>(WorkflowLinesManager);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const containerRef = useRef<HTMLDivElement>(null);
  const propsRef = useRef(props);
  const latestSchemaRef = useRef(props.schema);
  const reloadingRef = useRef(false);
  const draggingRef = useRef(false);
  const lastWorkflowSignatureRef = useRef<string>();
  const initialViewportFitDoneRef = useRef(false);
  const [dropActive, setDropActive] = useState(false);
  propsRef.current = props;
  latestSchemaRef.current = props.schema;

  const normalizedWorkflow = useMemo(
    () => normalizeWorkflow(props.schema.workflow as WorkflowJSON),
    [props.schema.workflow],
  );
  const gridEnabled = props.schema.editor.gridEnabled !== false;
  const miniMapVisible = props.schema.editor.showMiniMap === true;

  const commitWorkflow = (workflow: WorkflowJSON, reason: string, options: { snapToGrid?: boolean } = {}) => {
    const nextWorkflow = normalizeWorkflow(workflow, options);
    const nextSignature = workflowSignature(nextWorkflow);
    if (nextSignature === workflowSignature(latestSchemaRef.current.workflow)) {
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
    const nextWorkflow = normalizeWorkflow({
      ...latestSchemaRef.current.workflow,
      edges: workflow.edges ?? [],
    } as WorkflowJSON);
    const nextSignature = workflowSignature(nextWorkflow);
    if (nextSignature === workflowSignature(latestSchemaRef.current.workflow)) {
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

  useEffect(() => {
    const config = playground.config as typeof playground.config & { readonly?: boolean };
    const previous = config.readonly;
    config.readonly = Boolean(props.readonly);
    return () => {
      config.readonly = previous;
    };
  }, [playground.config, props.readonly]);

  useEffect(() => {
    const nextSignature = workflowSignature(normalizedWorkflow);
    const currentDocSignature = workflowSignature(normalizeWorkflow(doc.toJSON() as WorkflowJSON));
    if (lastWorkflowSignatureRef.current === nextSignature && currentDocSignature === nextSignature) {
      return;
    }
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(cloneWorkflow(normalizedWorkflow))).finally(() => {
      lastWorkflowSignatureRef.current = nextSignature;
      reloadingRef.current = false;
    });
  }, [doc, normalizedWorkflow]);

  useEffect(() => {
    const disposable = dragService.onNodesDrag(event => {
      if (propsRef.current.readonly) {
        return;
      }
      if (event.type === "onDragStart") {
        draggingRef.current = true;
        return;
      }
      if (event.type === "onDragEnd") {
        draggingRef.current = false;
        commitWorkflow(doc.toJSON() as WorkflowJSON, "flowgramNodeMove", { snapToGrid: propsRef.current.schema.editor.gridEnabled !== false });
      }
    });
    return () => disposable.dispose();
  }, [doc, dragService]);

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

  const dropPointFromClient = (clientX: number, clientY: number, nativeEvent?: globalThis.MouseEvent): MicroflowPoint => {
    const rect = containerRef.current?.getBoundingClientRect();
    const viewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const localX = rect ? clientX - rect.left : clientX;
    const localY = rect ? clientY - rect.top : clientY;
    const fallback = {
      x: (localX - viewport.x) / Math.max(0.2, viewport.zoom),
      y: (localY - viewport.y) / Math.max(0.2, viewport.zoom),
    };
    const dragPosition = nativeEvent
      ? playground.config.getPosFromMouseEvent?.(nativeEvent) as MicroflowPoint | undefined
      : undefined;
    const position = dragPosition ?? fallback;
    return gridEnabled
      ? {
          x: Math.round(position.x / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
          y: Math.round(position.y / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
        }
      : position;
  };

  const dropPointFromEvent = (event: DragEvent<HTMLDivElement>): MicroflowPoint =>
    dropPointFromClient(event.clientX, event.clientY, event.nativeEvent);

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    event.dataTransfer.dropEffect = "copy";
    setDropActive(true);
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    setDropActive(false);
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
    props.onDropRegistryItem?.(item, dropPointFromEvent(event), payload);
  };

  const handlePointerFallbackDrop = (event: MouseEvent<HTMLDivElement>) => {
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
    props.onDropRegistryItem?.(item, dropPointFromClient(event.clientX, event.clientY, event.nativeEvent), payload);
  };

  const openContextMenuFromTarget = (target: HTMLElement | undefined, point: { x: number; y: number }): boolean => {
    const edgeElement = target?.closest<HTMLElement>("[data-flow-id]");
    if (edgeElement && containerRef.current?.contains(edgeElement)) {
      const flowId = edgeElement.dataset.flowId;
      if (flowId) {
        const selection = { objectId: undefined, flowId, collectionId: undefined, objectIds: [], flowIds: [flowId], mode: "single" as const };
        props.onSelectionChange(selection);
        props.onNodeContextMenu?.(selection, point);
        return true;
      }
    }
    const nodeElement = target?.closest<HTMLElement>(".microflow-flowgram-node[data-microflow-object-id]");
    if (!nodeElement || !containerRef.current?.contains(nodeElement)) {
      return false;
    }
    const objectId = nodeElement.dataset.microflowObjectId;
    if (!objectId) {
      return false;
    }
    const selection = { objectId, flowId: undefined, collectionId: MICROFLOW_ROOT_COLLECTION_ID, objectIds: [objectId], flowIds: [], mode: "single" as const };
    props.onSelectionChange(selection);
    props.onNodeContextMenu?.(selection, point);
    return true;
  };

  const handleMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button !== 2) {
      return;
    }
    const target = event.target instanceof HTMLElement ? event.target : undefined;
    if (openContextMenuFromTarget(target, { x: event.clientX, y: event.clientY })) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  const handleContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    const target = event.target instanceof HTMLElement ? event.target : undefined;
    if (openContextMenuFromTarget(target, { x: event.clientX, y: event.clientY })) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target instanceof HTMLElement ? event.target : undefined;
    if (
      target?.closest(
        ".microflow-flowgram-node, .microflow-flowgram-canvas-controls, .microflow-flowgram-toolbar, .microflow-flowgram-status-strip, .microflow-flowgram-minimap, .semi-popover, .semi-dropdown, .semi-modal",
      )
    ) {
      return;
    }
    props.onCanvasBlankClick?.();
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

  useEffect(() => {
    if (initialViewportFitDoneRef.current) {
      return;
    }
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect || !props.schema.workflow.nodes.length) {
      return;
    }
    initialViewportFitDoneRef.current = true;
    props.onViewportChange?.(fitViewportForWorkflow(props.schema.workflow, rect), { skipDirty: true });
  }, [props.schema.workflow, props.onViewportChange]);

  useEffect(() => {
    if (props.focusObjectId) {
      focusNode(props.focusObjectId);
    }
  }, [props.focusObjectId, props.focusRequestKey]);

  const registerDraftValidator = useCallback(
    (fn: (() => { valid: boolean; summary: string }) | null) => props.registerDraftValidator?.(fn),
    [props.registerDraftValidator],
  );

  const contextValue = useMemo(() => ({
    schema: props.schema,
    variableIndex,
    getVariablesForNode,
    runtimeTraceByObjectId,
    expandedObjectId: effectiveExpandedObjectId,
    onExpandChange: effectiveOnExpandChange,
    onSchemaChange: props.onSchemaChange,
    readonly: Boolean(props.readonly),
    registerDraftValidator,
  }), [props.schema, variableIndex, getVariablesForNode, runtimeTraceByObjectId, effectiveExpandedObjectId, effectiveOnExpandChange, props.onSchemaChange, props.readonly, registerDraftValidator]);

  return (
    <FlowGramMicroflowRuntimeContext.Provider value={contextValue}>
    <div
      ref={containerRef}
      className={`microflow-flowgram-canvas${dropActive ? " is-drop-active" : ""}${gridEnabled ? "" : " is-grid-hidden"}`}
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
        }
      }}
      onDropCapture={handleDrop}
      onMouseDownCapture={handleMouseDown}
      onMouseUpCapture={handlePointerFallbackDrop}
      onContextMenuCapture={handleContextMenu}
      onPointerDownCapture={handlePointerDown}
    >
      <PlaygroundReactRenderer />
      <div className="microflow-flowgram-canvas-controls">
        <FlowGramMicroflowStatusStrip
          validationIssues={props.validationIssues}
          dirty={props.dirty ?? false}
          saving={props.saving ?? false}
          validating={props.validating ?? false}
          readonly={props.readonly}
          onOpenProblemsPanel={props.onOpenProblemsPanel}
        />
        <FlowGramMicroflowToolbar
          canUndo={props.canUndo}
          canRedo={props.canRedo}
          onUndo={props.onUndo}
          onRedo={props.onRedo}
          onAutoLayout={props.onAutoLayout}
          readonly={props.readonly}
          viewport={props.schema.editor.viewport}
          onViewportChange={viewport => props.onViewportChange?.(viewport)}
          onFitView={fitViewportToWorkflow}
          gridEnabled={gridEnabled}
          onToggleGrid={() => props.onToggleGrid?.(!gridEnabled)}
          miniMapVisible={miniMapVisible}
          onToggleMiniMap={() => props.onToggleMiniMap?.(!miniMapVisible)}
        />
      </div>
      {miniMapVisible ? <FlowGramMicroflowNativeMiniMap schema={props.schema} onFocusNode={focusNode} /> : null}
    </div>
    </FlowGramMicroflowRuntimeContext.Provider>
  );
}

export function FlowGramMicroflowNativeCanvas(props: FlowGramMicroflowNativeCanvasProps) {
  const structureKey = workflowRenderStructureKey(props.schema.workflow);
  return (
    <FlowGramMicroflowProvider key={structureKey}>
      <FlowGramMicroflowNativeCanvasInner {...props} />
    </FlowGramMicroflowProvider>
  );
}
