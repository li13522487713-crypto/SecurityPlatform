import { useMemo, useRef, useState, type DragEvent } from "react";

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
import type { MicroflowPoint, MicroflowSchema, MicroflowTraceFrame, MicroflowValidationIssue } from "../schema";
import { toEditorGraph } from "../adapters";
import { FlowGramMicroflowCaseEditor } from "./FlowGramMicroflowCaseEditor";
import { FlowGramMicroflowProvider } from "./FlowGramMicroflowProvider";
import { FlowGramMicroflowToolbar } from "./FlowGramMicroflowToolbar";
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
  readonly?: boolean;
  onSchemaChange: (nextSchema: MicroflowSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onDropRegistryItem?: (item: MicroflowNodeRegistryItem, position: MicroflowPoint, payload: MicroflowNodeDragPayload) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
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

function FlowGramMicroflowCanvasInner(props: FlowGramMicroflowCanvasProps) {
  const playground = usePlayground();
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const containerRef = useRef<HTMLDivElement>(null);
  const [pendingCaseLine, setPendingCaseLine] = useState<FlowGramMicroflowPendingLine>();
  const [miniMapVisible, setMiniMapVisible] = useState(true);
  const [dropActive, setDropActive] = useState(false);
  const bridge = useFlowGramMicroflowBridge({
    schema: props.schema,
    issues: props.validationIssues,
    traceFrames: props.runtimeTrace,
    readonly: props.readonly,
    onSchemaChange: props.onSchemaChange,
    onSelectionChange: props.onSelectionChange,
    onPendingCaseLine: setPendingCaseLine,
  });
  const options = useMemo(
    () => pendingCaseLine ? getCaseOptionsForSource(props.schema, pendingCaseLine.sourceObjectId) : [],
    [pendingCaseLine, props.schema],
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
    props.onDropRegistryItem?.(item, dropPointFromEvent(event), payload);
  };

  const focusNodeFromMiniMap = (objectId: string) => {
    const node = playground.entityManager.getEntityById<FlowNodeEntity>(objectId);
    if (node) {
      void selectService.selectNodeAndScrollToView(node, true);
    }
    props.onSelectionChange({ objectId, flowId: undefined });
  };

  return (
    <div
      ref={containerRef}
      className={`microflow-flowgram-canvas${dropActive ? " is-drop-active" : ""}`}
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
        miniMapVisible={miniMapVisible}
        onToggleMiniMap={() => setMiniMapVisible(value => !value)}
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
