import { useMemo, useState, type DragEvent } from "react";

import { Button, Space } from "@douyinfe/semi-ui";
import { IconMinus, IconPlus, IconRefresh, IconUndo, IconRedo, IconTreeTriangleDown, IconMapPin } from "@douyinfe/semi-icons";
import {
  PlaygroundReactRenderer,
  WorkflowResetLayoutService,
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";
import { WorkflowRenderProvider } from "@coze-workflow/render";

import { microflowNodeRegistryByKey, type MicroflowNodeDragPayload, type MicroflowNodeRegistryItem } from "../node-registry";
import type { MicroflowPoint, MicroflowSchema, MicroflowTraceFrame, MicroflowValidationIssue } from "../schema";
import { toEditorGraph } from "../adapters";
import { FlowGramMicroflowContainerModule } from "./FlowGramMicroflowPlugins";
import { FlowGramMicroflowCaseEditor } from "./FlowGramMicroflowCaseEditor";
import { getCaseOptionsForSource } from "./adapters/flowgram-case-options";
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
  onDropRegistryItem?: (item: MicroflowNodeRegistryItem, position: MicroflowPoint) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
}

interface FlowGramMicroflowToolbarProps extends Pick<FlowGramMicroflowCanvasProps, "canUndo" | "canRedo" | "onUndo" | "onRedo" | "onAutoLayout" | "readonly"> {
  miniMapVisible: boolean;
  onToggleMiniMap: () => void;
}

function FlowGramMicroflowToolbar(props: FlowGramMicroflowToolbarProps) {
  const playground = usePlayground();
  const resetLayout = useService<WorkflowResetLayoutService>(WorkflowResetLayoutService);
  return (
    <div className="microflow-flowgram-toolbar">
      <Space>
        <Button icon={<IconPlus />} size="small" onClick={() => playground.config.zoomin()} />
        <Button icon={<IconMinus />} size="small" onClick={() => playground.config.zoomout()} />
        <Button icon={<IconRefresh />} size="small" onClick={() => resetLayout.fitView()} />
        <Button icon={<IconUndo />} size="small" disabled={!props.canUndo} onClick={props.onUndo} />
        <Button icon={<IconRedo />} size="small" disabled={!props.canRedo} onClick={props.onRedo} />
        <Button
          icon={<IconMapPin />}
          size="small"
          theme={props.miniMapVisible ? "solid" : "light"}
          onClick={props.onToggleMiniMap}
        />
        <Button icon={<IconTreeTriangleDown />} size="small" disabled={props.readonly} onClick={() => {
          props.onAutoLayout?.();
          requestAnimationFrame(() => resetLayout.fitView());
        }}>
          Auto
        </Button>
      </Space>
    </div>
  );
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

function snapPoint(point: MicroflowPoint, gridSize = 16): MicroflowPoint {
  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize,
  };
}

function FlowGramMicroflowMiniMap({ schema }: { schema: MicroflowSchema }) {
  const graph = useMemo(() => toEditorGraph(schema), [schema]);
  const rootNodes = graph.nodes.filter(node => !node.parentObjectId);
  const bounds = useMemo(() => {
    if (rootNodes.length === 0) {
      return { minX: 0, minY: 0, width: 1, height: 1 };
    }
    const minX = Math.min(...rootNodes.map(node => node.position.x - node.size.width / 2));
    const minY = Math.min(...rootNodes.map(node => node.position.y - node.size.height / 2));
    const maxX = Math.max(...rootNodes.map(node => node.position.x + node.size.width / 2));
    const maxY = Math.max(...rootNodes.map(node => node.position.y + node.size.height / 2));
    return {
      minX,
      minY,
      width: Math.max(1, maxX - minX),
      height: Math.max(1, maxY - minY),
    };
  }, [rootNodes]);
  const viewBox = `${bounds.minX - 80} ${bounds.minY - 80} ${bounds.width + 160} ${bounds.height + 160}`;
  const viewport = graph.viewport;
  const viewportWidth = 520 / Math.max(0.2, viewport.zoom);
  const viewportHeight = 320 / Math.max(0.2, viewport.zoom);

  return (
    <div className="microflow-flowgram-minimap" aria-label="Microflow minimap">
      <svg viewBox={viewBox} role="img">
        {graph.edges.map(edge => {
          const source = rootNodes.find(node => `node-${node.objectId}` === edge.sourceNodeId);
          const target = rootNodes.find(node => `node-${node.objectId}` === edge.targetNodeId);
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

function FlowGramMicroflowCanvasInner(props: FlowGramMicroflowCanvasProps) {
  const playground = usePlayground();
  const [pendingCaseLine, setPendingCaseLine] = useState<FlowGramMicroflowPendingLine>();
  const [miniMapVisible, setMiniMapVisible] = useState(true);
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
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    const payload = readNodeDragPayload(event.dataTransfer);
    if (!payload || props.readonly) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item) {
      return;
    }
    const rawPosition = playground.config.getPosFromMouseEvent(event.nativeEvent);
    props.onDropRegistryItem?.(item, snapPoint({ x: rawPosition.x, y: rawPosition.y }));
  };

  return (
    <div className="microflow-flowgram-canvas" onDragOver={handleDragOver} onDrop={handleDrop}>
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
      {miniMapVisible ? <FlowGramMicroflowMiniMap schema={props.schema} /> : null}
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
    <WorkflowRenderProvider containerModules={[FlowGramMicroflowContainerModule]}>
      <FlowGramMicroflowCanvasInner {...props} />
    </WorkflowRenderProvider>
  );
}
