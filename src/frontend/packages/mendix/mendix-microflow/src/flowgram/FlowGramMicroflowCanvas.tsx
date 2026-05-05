import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent, type MouseEvent, type PointerEvent } from "react";

import { Toast } from "@douyinfe/semi-ui";
import {
  FlowNodeTransformData,
  WorkflowDocument,
  WorkflowDragService,
  type FlowNodeEntity,
  PlaygroundReactRenderer,
  type WorkflowEdgeJSON,
  type WorkflowJSON,
  type WorkflowLineEntity,
  WorkflowLinesManager,
  type WorkflowNodeEntity,
  WorkflowSelectService,
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";

import {
  canDragRegistryItem,
  canConnectPorts,
  canCreateRegistryItem,
  getDisabledDragReason,
  hasMicroflowNodeDragType,
  microflowNodeRegistryByKey,
  readMicroflowNodeDragPayload,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
import type { MicroflowTraceFrame } from "../debug/trace-types";
import type {
  MicroflowCaseValue,
  MicroflowEditorGraphPatch,
  MicroflowPoint,
  MicroflowSchema,
  MicroflowValidationIssue,
} from "../schema";
import { applyEditorGraphPatchToAuthoring, toEditorGraph } from "../adapters";
import { FlowGramMicroflowCaseEditor } from "./FlowGramMicroflowCaseEditor";
import { FlowGramMicroflowProvider } from "./FlowGramMicroflowProvider";
import { FlowGramMicroflowStatusStrip } from "./FlowGramMicroflowStatusStrip";
import { FlowGramMicroflowToolbar, microflowZoomViewportAtCanvasCenter } from "./FlowGramMicroflowToolbar";
import { useMicroflowMetadataCatalog } from "../metadata";
import { getCaseEditorKind, getCaseOptionsForSource } from "./adapters/flowgram-case-options";
import { authoringToFlowGram } from "./adapters/authoring-to-flowgram";
import {
  clientPointToFlowGramPoint,
  flowGramPointToAuthoringPoint,
  getFlowGramCanvasContainerRect,
  MICROFLOW_GRID_SIZE,
  normalizeFlowGramPoint,
  snapMicroflowPoint,
} from "./adapters/flowgram-coordinate";
import {
  flowGramEdgeIdentitySignature,
  flowGramNodeIdentitySignature,
  flowGramPositionSignature,
  toFlowGramNodeId,
  toMicroflowObjectId,
} from "./adapters/flowgram-identity";
import {
  createFlowFromFlowGramEdge,
  findDeletedFlowId,
  findDeletedObjectId,
  findNewFlowGramEdge,
  flowGramPositionPatch,
} from "./adapters/flowgram-to-authoring-patch";
import { selectionFromFlowGramEntityIds } from "./adapters/flowgram-selection-sync";
import { createMicroflowFlowFromPorts } from "./adapters/flowgram-edge-factory";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";
import "@flowgram-adapter/free-layout-editor/css-load";
import "./styles/flowgram-microflow-canvas.css";
import "./styles/flowgram-microflow-port.css";
import "./styles/flowgram-microflow-line.css";

export interface FlowGramMicroflowCanvasProps {
  schema: MicroflowSchema;
  validationIssues: MicroflowValidationIssue[];
  runtimeTrace: MicroflowTraceFrame[];
  focusObjectId?: string;
  focusRequestKey?: number;
  readonly?: boolean;
  onSchemaChange: (nextSchema: MicroflowSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onCanvasBlankClick?: () => void;
  onNodeContextMenu?: (selection: FlowGramMicroflowSelection, point: { x: number; y: number }) => void;
  onDropRegistryItem?: (
    item: MicroflowNodeRegistryItem,
    position: MicroflowPoint,
    payload: MicroflowNodeDragPayload,
    options?: { parentLoopObjectId?: string },
  ) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
  onViewportChange?: (viewport: MicroflowSchema["editor"]["viewport"], options?: { skipDirty?: boolean }) => void;
  onToggleMiniMap?: (visible: boolean) => void;
  onToggleGrid?: (enabled: boolean) => void;
  dirty?: boolean;
  saving?: boolean;
  validating?: boolean;
  onOpenProblemsPanel?: () => void;
}

function FlowGramMicroflowMiniMap({ schema, onFocusNode }: { schema: MicroflowSchema; onFocusNode: (objectId: string) => void }) {
  const graph = useMemo(() => toEditorGraph(schema), [schema]);
  const rootNodes = graph.nodes.filter(node => !node.parentObjectId);
  const lodStep = graph.nodes.length > 500 ? Math.ceil(graph.nodes.length / 500) : 1;
  const minimapNodes = useMemo(() => rootNodes.filter((_, index) => index % lodStep === 0), [lodStep, rootNodes]);
  const minimapNestedNodes = useMemo(() => graph.nodes.filter(node => node.parentObjectId).filter((_, index) => index % lodStep === 0), [graph.nodes, lodStep]);
  const minimapEdges = useMemo(() => graph.edges.filter((_, index) => index % lodStep === 0), [graph.edges, lodStep]);
  const nodeById = useMemo(() => new Map(graph.nodes.map(node => [node.objectId, node])), [graph.nodes]);
  const bounds = useMemo(() => {
    if (graph.nodes.length === 0) {
      return { minX: 0, minY: 0, width: 1, height: 1 };
    }
    const minX = Math.min(...graph.nodes.map(node => node.position.x - node.size.width / 2));
    const minY = Math.min(...graph.nodes.map(node => node.position.y - node.size.height / 2));
    const maxX = Math.max(...graph.nodes.map(node => node.position.x + node.size.width / 2));
    const maxY = Math.max(...graph.nodes.map(node => node.position.y + node.size.height / 2));
    return {
      minX,
      minY,
      width: Math.max(1, maxX - minX),
      height: Math.max(1, maxY - minY),
    };
  }, [graph.nodes]);
  const viewBox = `${bounds.minX - 80} ${bounds.minY - 80} ${bounds.width + 160} ${bounds.height + 160}`;
  const viewport = graph.viewport;
  const viewportWidth = 520 / Math.max(0.2, viewport.zoom);
  const viewportHeight = 320 / Math.max(0.2, viewport.zoom);

  return (
    <div className="microflow-flowgram-minimap" aria-label="Microflow minimap">
      <svg viewBox={viewBox} role="img">
        {minimapEdges.map(edge => {
          const source = nodeById.get(toMicroflowObjectId(edge.sourceNodeId));
          const target = nodeById.get(toMicroflowObjectId(edge.targetNodeId));
          if (!source || !target) {
            return null;
          }
          return (
            <line
              key={edge.flowId}
              x1={source.position.x}
              y1={source.position.y}
              x2={target.position.x}
              y2={target.position.y}
              className={`microflow-flowgram-minimap-edge microflow-flowgram-minimap-edge-${edge.edgeKind}`}
            />
          );
        })}
        {minimapNodes.map(node => (
          <rect
            key={node.objectId}
            x={node.position.x - node.size.width / 2}
            y={node.position.y - node.size.height / 2}
            width={node.size.width}
            height={node.size.height}
            rx={node.nodeKind === "exclusiveSplit" || node.nodeKind === "inheritanceSplit" ? 4 : 10}
            className={`microflow-flowgram-minimap-node microflow-flowgram-minimap-node-${node.nodeKind}`}
            role="button"
            tabIndex={0}
            onClick={() => onFocusNode(node.objectId)}
            onKeyDown={event => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                onFocusNode(node.objectId);
              }
            }}
          />
        ))}
        {minimapNestedNodes.map(node => (
          <rect
            key={node.objectId}
            x={node.position.x - Math.min(node.size.width, 120) / 2}
            y={node.position.y - Math.min(node.size.height, 72) / 2}
            width={Math.min(node.size.width, 120)}
            height={Math.min(node.size.height, 72)}
            rx={6}
            className={`microflow-flowgram-minimap-node microflow-flowgram-minimap-node-nested microflow-flowgram-minimap-node-${node.nodeKind}`}
            role="button"
            tabIndex={0}
            onClick={() => onFocusNode(node.objectId)}
            onKeyDown={event => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                onFocusNode(node.objectId);
              }
            }}
          />
        ))}
        {lodStep > 1 ? (
          <text x={bounds.minX} y={bounds.minY - 24} className="microflow-flowgram-minimap-lod">
            LOD {graph.nodes.length}
          </text>
        ) : null}
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

export function getLoopContainerByClientPoint(
  root: HTMLElement | null,
  point: { x: number; y: number },
): { loopObjectId: string; isBody: boolean } | undefined {
  const element = root?.ownerDocument.elementFromPoint(point.x, point.y);
  if (!(element instanceof HTMLElement)) {
    return undefined;
  }
  const loopNode = element.closest<HTMLElement>(".microflow-flowgram-node--loop[data-microflow-object-id]");
  if (!loopNode || !root?.contains(loopNode)) {
    return undefined;
  }
  const loopObjectId = loopNode.dataset.microflowObjectId;
  if (!loopObjectId) {
    return undefined;
  }
  return {
    loopObjectId,
    isBody: Boolean(element.closest("[data-microflow-loop-body='true']")),
  };
}

function hasUsableViewport(viewport: MicroflowSchema["editor"]["viewport"] | undefined): viewport is NonNullable<MicroflowSchema["editor"]["viewport"]> {
  return Boolean(
    viewport
      && Number.isFinite(viewport.x)
      && Number.isFinite(viewport.y)
      && Number.isFinite(viewport.zoom)
      && viewport.zoom >= 0.2
      && viewport.zoom <= 2,
  );
}

function graphBounds(graph: ReturnType<typeof toEditorGraph>) {
  if (graph.nodes.length === 0) {
    return undefined;
  }
  const minX = Math.min(...graph.nodes.map(node => node.position.x - node.size.width / 2));
  const minY = Math.min(...graph.nodes.map(node => node.position.y - node.size.height / 2));
  const maxX = Math.max(...graph.nodes.map(node => node.position.x + node.size.width / 2));
  const maxY = Math.max(...graph.nodes.map(node => node.position.y + node.size.height / 2));
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: Math.max(1, maxX - minX),
    height: Math.max(1, maxY - minY),
  };
}

function viewportContainsGraph(
  graph: ReturnType<typeof toEditorGraph>,
  rect: Pick<DOMRect, "width" | "height">,
  viewport: NonNullable<MicroflowSchema["editor"]["viewport"]>,
) {
  const bounds = graphBounds(graph);
  if (!bounds) {
    return true;
  }
  const padding = 48;
  const left = bounds.minX * viewport.zoom + viewport.x;
  const top = bounds.minY * viewport.zoom + viewport.y;
  const right = bounds.maxX * viewport.zoom + viewport.x;
  const bottom = bounds.maxY * viewport.zoom + viewport.y;
  return left >= padding
    && top >= padding
    && right <= rect.width - padding
    && bottom <= rect.height - padding;
}

function fitViewportForGraph(graph: ReturnType<typeof toEditorGraph>, rect: Pick<DOMRect, "width" | "height">): MicroflowSchema["editor"]["viewport"] {
  const bounds = graphBounds(graph);
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

function hasSameNodeAndEdgeIdentity(a: WorkflowJSON | undefined, b: WorkflowJSON | undefined): boolean {
  return flowGramNodeIdentitySignature(a) === flowGramNodeIdentitySignature(b)
    && flowGramEdgeIdentitySignature(a) === flowGramEdgeIdentitySignature(b);
}

function workflowIdentity(json: WorkflowJSON): string {
  return JSON.stringify({
    nodes: flowGramNodeIdentitySignature(json),
    edges: flowGramEdgeIdentitySignature(json),
  });
}

function movedNodeSignature(patch: MicroflowEditorGraphPatch): string {
  return JSON.stringify((patch.movedNodes ?? [])
    .map(node => `${node.objectId}:${node.position.x}:${node.position.y}`)
    .sort());
}

function resizedNodeSignature(patch: MicroflowEditorGraphPatch): string {
  return JSON.stringify((patch.resizedNodes ?? [])
    .map(node => `${node.objectId}:${node.size.width}:${node.size.height}`)
    .sort());
}

function syncFlowGramPositionsToSchema(doc: WorkflowDocument, schema: MicroflowSchema): boolean {
  const graph = toEditorGraph(schema);
  let synced = false;
  for (const node of graph.nodes) {
    const entity = doc.getNode?.(toFlowGramNodeId(node.objectId)) as WorkflowNodeEntity | undefined;
    const transform = entity?.getData?.(FlowNodeTransformData);
    if (!transform) {
      continue;
    }
    const current = transform.position ?? transform.bounds;
    if (!current) {
      continue;
    }
    if (Math.abs(current.x - node.position.x) <= 0.5 && Math.abs(current.y - node.position.y) <= 0.5) {
      continue;
    }
    transform.transform.update({
      position: {
        x: node.position.x,
        y: node.position.y,
      },
    });
    synced = true;
  }
  return synced;
}

function portById(schema: MicroflowSchema, portId?: string) {
  if (!portId) {
    return undefined;
  }
  return toEditorGraph(schema).nodes.flatMap(node => node.ports).find(port => port.id === portId);
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

function lineMatchesEdge(line: WorkflowLineEntity, edge: WorkflowEdgeJSON): boolean {
  const candidate = line as DisposableLineCandidate;
  const json = (line as { toJSON?: () => DisposableLineSnapshot }).toJSON?.();
  const edgeCandidate = edge as WorkflowEdgeJSON & { id?: string | number; data?: { flowId?: string } };
  const edgeId = edgeCandidate.id === undefined ? undefined : String(edgeCandidate.id);
  const edgeFlowId = edgeCandidate.data?.flowId;
  if (
    (edgeId && (String(candidate.id) === edgeId || String(json?.id) === edgeId))
    || (edgeFlowId && (candidate.data?.flowId === edgeFlowId || json?.data?.flowId === edgeFlowId))
  ) {
    return true;
  }
  const sourceNodeId = edge.sourceNodeID === undefined ? undefined : String(edge.sourceNodeID);
  const targetNodeId = edge.targetNodeID === undefined ? undefined : String(edge.targetNodeID);
  const sourcePortId = edge.sourcePortID === undefined ? undefined : String(edge.sourcePortID);
  const targetPortId = edge.targetPortID === undefined ? undefined : String(edge.targetPortID);
  return Boolean(
    sourceNodeId
      && targetNodeId
      && String(candidate.info?.from ?? json?.sourceNodeID ?? "") === sourceNodeId
      && String(candidate.info?.to ?? json?.targetNodeID ?? "") === targetNodeId
      && String(candidate.info?.fromPort ?? json?.sourcePortID ?? "") === (sourcePortId ?? "")
      && String(candidate.info?.toPort ?? json?.targetPortID ?? "") === (targetPortId ?? ""),
  );
}

function FlowGramMicroflowCanvasInner(props: FlowGramMicroflowCanvasProps) {
  type FlowGramChangeKind = "nodePosition" | "nodeStructure" | "edgeStructure";

  type FlowGramPlaygroundViewportConfig = {
    zoom?: number | ((zoom: number) => void);
    updateConfig?: (config: { zoom?: number; scrollX?: number; scrollY?: number }) => void;
  };

  const playground = usePlayground();
  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const dragService = useService<WorkflowDragService>(WorkflowDragService);
  const linesManager = useService<WorkflowLinesManager>(WorkflowLinesManager);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const containerRef = useRef<HTMLDivElement>(null);
  const initialViewportFitDoneRef = useRef(false);
  const reloadingFromSchemaRef = useRef(false);
  const draggingNodeRef = useRef(false);
  const applyingPositionPatchRef = useRef(false);
  const lastWorkflowIdentityRef = useRef<string>();
  const lastPositionSignatureRef = useRef<string>();
  const positionSettleTimerRef = useRef<number>();
  const latestSchemaRef = useRef(props.schema);
  const propsRef = useRef(props);
  const lastAppliedPatchSignatureRef = useRef<string>();
  const [pendingCaseLine, setPendingCaseLine] = useState<FlowGramMicroflowPendingLine>();
  const [dropActive, setDropActive] = useState(false);
  const miniMapVisible = props.schema.editor.showMiniMap === true;
  const gridEnabled = props.schema.editor.gridEnabled !== false;

  const applyZoomFromCanvasCenter = useCallback((normalizedZoom: number) => {
    const root = containerRef.current;
    const v = propsRef.current.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const rect = root?.getBoundingClientRect();
    const next = microflowZoomViewportAtCanvasCenter(v, rect?.width ?? 0, rect?.height ?? 0, normalizedZoom);
    const config = playground.config as unknown as FlowGramPlaygroundViewportConfig;
    if (typeof config.zoom === "function") {
      config.zoom(next.zoom);
    }
    config.updateConfig?.({ zoom: next.zoom, scrollX: next.x, scrollY: next.y });
    propsRef.current.onViewportChange?.(next);
  }, [playground]);

  propsRef.current = props;
  latestSchemaRef.current = props.schema;
  const workflowJson = useMemo(
    () => authoringToFlowGram(props.schema, props.validationIssues, props.runtimeTrace),
    [props.schema.flows, props.schema.objectCollection, props.validationIssues, props.runtimeTrace],
  );
  const metadataCatalog = useMicroflowMetadataCatalog();
  const options = useMemo(
    () => pendingCaseLine && metadataCatalog
      ? getCaseOptionsForSource(props.schema, pendingCaseLine.sourceObjectId, undefined, metadataCatalog)
      : [],
    [pendingCaseLine, props.schema, metadataCatalog],
  );

  const rememberSchemaPositionSignature = (schema: MicroflowSchema) => {
    lastPositionSignatureRef.current = flowGramPositionSignature(
      authoringToFlowGram(schema, propsRef.current.validationIssues, propsRef.current.runtimeTrace),
    );
  };

  const reloadFromSchema = (schema: MicroflowSchema) => {
    const nextJson = authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace);
    const nextIdentity = workflowIdentity(nextJson);
    const nextPositionSignature = flowGramPositionSignature(nextJson);
    reloadingFromSchemaRef.current = true;
    void Promise.resolve(doc.fromJSON(nextJson)).finally(() => {
      lastWorkflowIdentityRef.current = nextIdentity;
      lastPositionSignatureRef.current = nextPositionSignature;
      reloadingFromSchemaRef.current = false;
    });
  };

  const releaseDragPositionGuardSoon = () => {
    if (positionSettleTimerRef.current !== undefined) {
      window.clearTimeout(positionSettleTimerRef.current);
    }
    positionSettleTimerRef.current = window.setTimeout(() => {
      positionSettleTimerRef.current = undefined;
      draggingNodeRef.current = false;
      applyingPositionPatchRef.current = false;
    }, 120);
  };

  const commitFinalDraggedPositions = () => {
    const schema = latestSchemaRef.current;
    const patch = flowGramPositionPatch(schema, doc.toJSON() as WorkflowJSON, {
      gridEnabled: schema.editor.gridEnabled !== false,
      gridSize: MICROFLOW_GRID_SIZE,
    });
    const patchSignature = `${movedNodeSignature(patch)}|${resizedNodeSignature(patch)}`;
    if (!(patch.movedNodes?.length || patch.resizedNodes?.length) || patchSignature === lastAppliedPatchSignatureRef.current) {
      releaseDragPositionGuardSoon();
      return;
    }
    const nextSchema = applyEditorGraphPatchToAuthoring(schema, patch);
    latestSchemaRef.current = nextSchema;
    rememberSchemaPositionSignature(nextSchema);
    lastAppliedPatchSignatureRef.current = patchSignature;
    applyingPositionPatchRef.current = true;
    syncFlowGramPositionsToSchema(doc, nextSchema);
    propsRef.current.onSchemaChange(nextSchema, "flowgramNodeMove");
    releaseDragPositionGuardSoon();
  };

  const createCaseFlow = (caseValues: MicroflowCaseValue[], label: string, pending: FlowGramMicroflowPendingLine) => {
    const sourcePort = portById(latestSchemaRef.current, pending.sourcePortId);
    const targetPort = portById(latestSchemaRef.current, pending.targetPortId);
    if (!sourcePort || !targetPort) {
      return;
    }
    const flow = createMicroflowFlowFromPorts(latestSchemaRef.current, sourcePort, targetPort, { caseValues, label });
    const nextSchema = applyEditorGraphPatchToAuthoring(latestSchemaRef.current, {
      addFlow: flow,
      selectedFlowId: flow.id,
      selectedObjectId: undefined,
    } as MicroflowEditorGraphPatch);
    latestSchemaRef.current = nextSchema;
    rememberSchemaPositionSignature(nextSchema);
    propsRef.current.onSchemaChange(nextSchema, "flowgramLineAdd");
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
    const nextIdentity = workflowIdentity(workflowJson);
    const nextPositionSignature = flowGramPositionSignature(workflowJson);
    if (lastWorkflowIdentityRef.current === nextIdentity && lastPositionSignatureRef.current === nextPositionSignature) {
      return;
    }
    if ((applyingPositionPatchRef.current || draggingNodeRef.current) && lastWorkflowIdentityRef.current === nextIdentity) {
      lastPositionSignatureRef.current = nextPositionSignature;
      return;
    }
    if (lastWorkflowIdentityRef.current === nextIdentity) {
      syncFlowGramPositionsToSchema(doc, props.schema);
      lastPositionSignatureRef.current = nextPositionSignature;
      return;
    }
    reloadingFromSchemaRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
      lastWorkflowIdentityRef.current = nextIdentity;
      lastPositionSignatureRef.current = nextPositionSignature;
      reloadingFromSchemaRef.current = false;
    });
  }, [doc, props.schema, workflowJson]);

  useEffect(() => () => {
    if (positionSettleTimerRef.current !== undefined) {
      window.clearTimeout(positionSettleTimerRef.current);
    }
  }, []);

  useEffect(() => {
    const disposable = dragService.onNodesDrag(event => {
      if (propsRef.current.readonly) {
        return;
      }
      if (event.type === "onDragStart") {
        draggingNodeRef.current = true;
        applyingPositionPatchRef.current = false;
        lastAppliedPatchSignatureRef.current = undefined;
        if (positionSettleTimerRef.current !== undefined) {
          window.clearTimeout(positionSettleTimerRef.current);
          positionSettleTimerRef.current = undefined;
        }
        return;
      }
      if (event.type === "onDragEnd") {
        commitFinalDraggedPositions();
      }
    });
    return () => disposable.dispose();
  }, [dragService]);

  useEffect(() => {
    const disposable = doc.onContentChange(() => {
      if (reloadingFromSchemaRef.current || propsRef.current.readonly) {
        return;
      }
      const json = doc.toJSON() as WorkflowJSON;
      const schema = latestSchemaRef.current;
      const currentJson = authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace);
      const changeKind: FlowGramChangeKind = !hasSameNodeAndEdgeIdentity(currentJson, json)
        ? flowGramNodeIdentitySignature(currentJson) !== flowGramNodeIdentitySignature(json)
          ? "nodeStructure"
          : "edgeStructure"
        : "nodePosition";
      if (changeKind === "nodePosition") {
        return;
      }
      const deletedObjectId = findDeletedObjectId(schema, json);
      if (deletedObjectId) {
        const patch = flowGramPositionPatch(schema, json, {
          gridEnabled: schema.editor.gridEnabled !== false,
          gridSize: MICROFLOW_GRID_SIZE,
        });
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          ...patch,
          deleteObjectId: deletedObjectId,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        rememberSchemaPositionSignature(nextSchema);
        propsRef.current.onSchemaChange(nextSchema, "flowgramNodeDelete");
        return;
      }
      const deletedFlowId = findDeletedFlowId(schema, json);
      if (deletedFlowId) {
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          deleteFlowId: deletedFlowId,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        rememberSchemaPositionSignature(nextSchema);
        propsRef.current.onSchemaChange(nextSchema, "flowgramLineDelete");
        return;
      }
      const newEdge = findNewFlowGramEdge(schema, json);
      if (newEdge) {
        const sourcePort = portById(schema, newEdge.sourcePortID === undefined ? undefined : String(newEdge.sourcePortID));
        const targetPort = portById(schema, newEdge.targetPortID === undefined ? undefined : String(newEdge.targetPortID));
        const check = sourcePort && targetPort ? canConnectPorts(schema, sourcePort, targetPort) : undefined;
        if (!sourcePort || !targetPort || !check?.allowed) {
          Toast.warning(check?.message ?? "The selected ports cannot be connected.");
          disposeTemporaryEdgeLine(newEdge);
          return;
        }
        const caseKind = getCaseEditorKind(schema, sourcePort.objectId);
        if (caseKind && (check.suggestedEdgeKind === "decisionCondition" || check.suggestedEdgeKind === "objectTypeCondition")) {
          setPendingCaseLine({
            caseKind,
            sourcePortId: sourcePort.id,
            targetPortId: targetPort.id,
            sourceObjectId: sourcePort.objectId,
            targetObjectId: targetPort.objectId,
          });
          disposeTemporaryEdgeLine(newEdge);
          return;
        }
        const flow = createFlowFromFlowGramEdge(schema, newEdge);
        if (!flow) {
          Toast.warning("无法识别连线端口，已回退本次连线。");
          disposeTemporaryEdgeLine(newEdge);
          return;
        }
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          addFlow: flow,
          selectedFlowId: flow.id,
          selectedObjectId: undefined,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        rememberSchemaPositionSignature(nextSchema);
        propsRef.current.onSchemaChange(nextSchema, "flowgramLineAdd");
        return;
      }
      reloadFromSchema(schema);
    });
    return () => disposable.dispose();
  }, [doc, linesManager]);

  useEffect(() => {
    const disposable = selectService.onSelectionChanged(() => {
      const ids = (selectService.selection ?? [])
        .map(selection => selection?.id)
        .filter((id): id is string => typeof id === "string" && id.length > 0);
      propsRef.current.onSelectionChange(selectionFromFlowGramEntityIds(latestSchemaRef.current, ids));
    });
    return () => disposable.dispose();
  }, [selectService]);

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (props.readonly || !hasMicroflowNodeDragType(event.dataTransfer)) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    event.dataTransfer.dropEffect = "copy";
    setDropActive(true);
  };

  const dropPointFromEvent = (event: DragEvent<HTMLDivElement>): MicroflowPoint => {
    const rect = getFlowGramCanvasContainerRect(containerRef.current);
    const viewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const fallback = normalizeFlowGramPoint(flowGramPointToAuthoringPoint(
      clientPointToFlowGramPoint({ x: event.clientX, y: event.clientY }, rect, viewport),
    )) ?? { x: 0, y: 0 };
    const dragPosition = normalizeFlowGramPoint(playground.config.getPosFromMouseEvent?.(event.nativeEvent));
    const position = dragPosition ?? fallback;
    return gridEnabled ? snapMicroflowPoint(position, MICROFLOW_GRID_SIZE) : position;
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
      console.warn(`Unknown microflow node registry key: ${payload.registryKey}`);
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
    const loopTarget = getLoopContainerByClientPoint(containerRef.current, { x: event.clientX, y: event.clientY });
    if (loopTarget && !loopTarget.isBody) {
      props.onSelectionChange({ objectId: loopTarget.loopObjectId, flowId: undefined });
      return;
    }
    props.onDropRegistryItem?.(item, dropPointFromEvent(event), payload, {
      parentLoopObjectId: loopTarget?.isBody ? loopTarget.loopObjectId : undefined,
    });
  };

  const handleContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    const target = event.target instanceof HTMLElement ? event.target : undefined;
    const nodeElement = target?.closest<HTMLElement>(".microflow-flowgram-node[data-microflow-object-id]");
    if (!nodeElement || !containerRef.current?.contains(nodeElement)) {
      return;
    }
    const objectId = nodeElement.dataset.microflowObjectId;
    if (!objectId) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const graphNode = toEditorGraph(props.schema).nodes.find(node => node.objectId === objectId);
    const selection = {
      objectId,
      flowId: undefined,
      collectionId: graphNode?.collectionId,
    };
    props.onSelectionChange(selection);
    props.onNodeContextMenu?.(selection, { x: event.clientX, y: event.clientY });
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

  const focusNodeFromMiniMap = (objectId: string) => {
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(toFlowGramNodeId(objectId));
    if (node) {
      void selectService.selectNodeAndScrollToView(node, true);
    }
    props.onSelectionChange({ objectId, flowId: undefined });
  };

  const fitViewportToSchema = () => {
    const graph = toEditorGraph(props.schema);
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect || graph.nodes.length === 0) {
      props.onViewportChange?.({ x: 0, y: 0, zoom: 1 });
      return;
    }
    props.onViewportChange?.(fitViewportForGraph(graph, rect));
  };

  useEffect(() => {
    if (initialViewportFitDoneRef.current) {
      return;
    }
    const graph = toEditorGraph(props.schema);
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect || graph.nodes.length === 0) {
      return;
    }
    const viewport = props.schema.editor.viewport;
    if (hasUsableViewport(viewport) && viewportContainsGraph(graph, rect, viewport)) {
      initialViewportFitDoneRef.current = true;
      return;
    }
    initialViewportFitDoneRef.current = true;
    props.onViewportChange?.(fitViewportForGraph(graph, rect), { skipDirty: true });
  }, [props.schema, props.onViewportChange]);

  useEffect(() => {
    if (!props.focusObjectId) {
      return;
    }
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(toFlowGramNodeId(props.focusObjectId));
    if (!node) {
      return;
    }
    void selectService.selectNodeAndScrollToView(node, true);
  }, [playground.entityManager, props.focusObjectId, props.focusRequestKey, selectService]);

  return (
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
          onFitView={fitViewportToSchema}
          gridEnabled={gridEnabled}
          onToggleGrid={() => props.onToggleGrid?.(!gridEnabled)}
          miniMapVisible={miniMapVisible}
          onToggleMiniMap={() => props.onToggleMiniMap?.(!miniMapVisible)}
          applyZoomFromCanvasCenter={applyZoomFromCanvasCenter}
        />
      </div>
      {miniMapVisible ? <FlowGramMicroflowMiniMap schema={props.schema} onFocusNode={focusNodeFromMiniMap} /> : null}
      <FlowGramMicroflowCaseEditor
        visible={Boolean(pendingCaseLine)}
        kind={pendingCaseLine?.caseKind ?? "boolean"}
        options={options}
        onCancel={() => setPendingCaseLine(undefined)}
        onConfirm={(caseValue, label) => {
          if (!pendingCaseLine) {
            return;
          }
          createCaseFlow([caseValue], label, pendingCaseLine);
          setPendingCaseLine(undefined);
        }}
      />
    </div>
  );
}

export function FlowGramMicroflowCanvas(props: FlowGramMicroflowCanvasProps) {
  return (
    <FlowGramMicroflowProvider>
      <FlowGramMicroflowCanvasInner {...props} />
    </FlowGramMicroflowProvider>
  );
}
