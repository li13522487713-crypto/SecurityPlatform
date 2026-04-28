import { useEffect, useMemo, useRef, useState, type DragEvent } from "react";

import { Toast } from "@douyinfe/semi-ui";
import {
  type FlowNodeEntity,
  PlaygroundReactRenderer,
  WorkflowSelectService,
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";

import {
  canDragRegistryItem,
  getDisabledDragReason,
  microflowNodeRegistryByKey,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowPoint, MicroflowSchema, MicroflowValidationIssue } from "../schema";
import { toEditorGraph } from "../adapters";
import { FlowGramMicroflowCaseEditor } from "./FlowGramMicroflowCaseEditor";
import { FlowGramMicroflowProvider } from "./FlowGramMicroflowProvider";
import { FlowGramMicroflowToolbar } from "./FlowGramMicroflowToolbar";
import { useMicroflowMetadataCatalog } from "../metadata";
import { getCaseOptionsForSource } from "./adapters/flowgram-case-options";
import {
  clientPointToFlowGramPoint,
  flowGramPointToAuthoringPoint,
  getFlowGramCanvasContainerRect,
  snapMicroflowPoint,
} from "./adapters/flowgram-coordinate";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";
import { useFlowGramMicroflowBridge } from "./hooks/useFlowGramMicroflowBridge";
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
  onDropRegistryItem?: (item: MicroflowNodeRegistryItem, position: MicroflowPoint, payload: MicroflowNodeDragPayload) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
  onViewportChange?: (viewport: MicroflowSchema["editor"]["viewport"]) => void;
  onToggleMiniMap?: (visible: boolean) => void;
  onToggleGrid?: (enabled: boolean) => void;
}

function readNodeDragPayload(dataTransfer: DataTransfer): MicroflowNodeDragPayload | undefined {
  const raw = dataTransfer.getData("application/x-atlas-microflow-node") || dataTransfer.getData("application/json");
  if (!raw) {
    return undefined;
  }
  try {
    const parsed = JSON.parse(raw) as MicroflowNodeDragPayload;
    return parsed.dragType === "microflow-node" ? parsed : undefined;
  } catch {
    return undefined;
  }
}

function FlowGramMicroflowMiniMap({ schema, onFocusNode }: { schema: MicroflowSchema; onFocusNode: (objectId: string) => void }) {
  const graph = useMemo(() => toEditorGraph(schema), [schema]);
  const rootNodes = graph.nodes.filter(node => !node.parentObjectId);
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
        {graph.edges.map(edge => {
          const source = nodeById.get(editorNodeIdToObjectId(edge.sourceNodeId));
          const target = nodeById.get(editorNodeIdToObjectId(edge.targetNodeId));
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
        {rootNodes.map(node => (
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
        {graph.nodes.filter(node => node.parentObjectId).map(node => (
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

function editorNodeIdToObjectId(editorNodeId: string): string {
  return editorNodeId.startsWith("node-") ? editorNodeId.slice(5) : editorNodeId;
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

function FlowGramMicroflowCanvasInner(props: FlowGramMicroflowCanvasProps) {
  const playground = usePlayground();
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const containerRef = useRef<HTMLDivElement>(null);
  const [pendingCaseLine, setPendingCaseLine] = useState<FlowGramMicroflowPendingLine>();
  const [dropActive, setDropActive] = useState(false);
  const miniMapVisible = props.schema.editor.showMiniMap !== false;
  const gridEnabled = props.schema.editor.gridEnabled !== false;
  const bridge = useFlowGramMicroflowBridge({
    schema: props.schema,
    issues: props.validationIssues,
    traceFrames: props.runtimeTrace,
    readonly: props.readonly,
    onSchemaChange: props.onSchemaChange,
    onSelectionChange: props.onSelectionChange,
    onPendingCaseLine: setPendingCaseLine,
  });
  const metadataCatalog = useMicroflowMetadataCatalog();
  const options = useMemo(
    () => pendingCaseLine && metadataCatalog
      ? getCaseOptionsForSource(props.schema, pendingCaseLine.sourceObjectId, undefined, metadataCatalog)
      : [],
    [pendingCaseLine, props.schema, metadataCatalog],
  );

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    const payload = readNodeDragPayload(event.dataTransfer);
    if (!payload || props.readonly) {
      return;
    }
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item || !canDragRegistryItem(item)) {
      event.dataTransfer.dropEffect = "none";
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
  };

  const dropPointFromEvent = (event: DragEvent<HTMLDivElement>): MicroflowPoint => {
    const rect = getFlowGramCanvasContainerRect(containerRef.current);
    const viewport = props.schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const fallback = flowGramPointToAuthoringPoint(
      clientPointToFlowGramPoint({ x: event.clientX, y: event.clientY }, rect, viewport),
    );
    const rawPosition = playground.config.getPosFromMouseEvent?.(event.nativeEvent) ?? fallback;
    return snapMicroflowPoint({ x: rawPosition.x, y: rawPosition.y });
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    setDropActive(false);
    const payload = readNodeDragPayload(event.dataTransfer);
    if (!payload || props.readonly) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item) {
      console.warn(`Unknown microflow node registry key: ${payload.registryKey}`);
      Toast.warning("Unknown microflow node type.");
      return;
    }
    if (!canDragRegistryItem(item)) {
      Toast.warning(getDisabledDragReason(item) ?? "This node cannot be added to Microflow.");
      return;
    }
    const loopTarget = getLoopContainerByClientPoint(containerRef.current, { x: event.clientX, y: event.clientY });
    if (loopTarget && !loopTarget.isBody) {
      props.onSelectionChange({ objectId: loopTarget.loopObjectId, flowId: undefined });
      return;
    }
    props.onDropRegistryItem?.(item, dropPointFromEvent(event), payload);
  };

  const focusNodeFromMiniMap = (objectId: string) => {
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(objectId);
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
    const minX = Math.min(...graph.nodes.map(node => node.position.x - node.size.width / 2));
    const minY = Math.min(...graph.nodes.map(node => node.position.y - node.size.height / 2));
    const maxX = Math.max(...graph.nodes.map(node => node.position.x + node.size.width / 2));
    const maxY = Math.max(...graph.nodes.map(node => node.position.y + node.size.height / 2));
    const width = Math.max(1, maxX - minX);
    const height = Math.max(1, maxY - minY);
    const zoom = Math.max(0.2, Math.min(1.2, Math.min((rect.width - 120) / width, (rect.height - 120) / height)));
    props.onViewportChange?.({
      x: Math.round(rect.width / 2 - (minX + width / 2) * zoom),
      y: Math.round(rect.height / 2 - (minY + height / 2) * zoom),
      zoom,
    });
  };

  useEffect(() => {
    if (!props.focusObjectId) {
      return;
    }
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(props.focusObjectId);
    if (!node) {
      return;
    }
    void selectService.selectNodeAndScrollToView(node, true);
  }, [playground.entityManager, props.focusObjectId, props.focusRequestKey, selectService]);

  return (
    <div
      ref={containerRef}
      className={`microflow-flowgram-canvas${dropActive ? " is-drop-active" : ""}${gridEnabled ? "" : " is-grid-hidden"}`}
      onDragEnter={event => {
        if (readNodeDragPayload(event.dataTransfer) && !props.readonly) {
          setDropActive(true);
        }
      }}
      onDragOver={handleDragOver}
      onDragLeave={event => {
        if (event.currentTarget === event.target) {
          setDropActive(false);
        }
      }}
      onDrop={handleDrop}
    >
      <PlaygroundReactRenderer />
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
      />
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
          bridge.createCaseFlow([caseValue], label, pendingCaseLine);
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
