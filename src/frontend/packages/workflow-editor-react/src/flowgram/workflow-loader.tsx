import { useCallback, useEffect, useMemo, useRef } from "react";
import {
  EditorRenderer,
  FreeLayoutEditorProvider,
  useClientContext,
  type WorkflowJSON,
  type WorkflowContentChangeEvent,
  type WorkflowNodeRegistry,
  type WorkflowPortEntity,
  type onDragLineEndParams,
  useService
} from "@flowgram.ai/free-layout-editor";
import { FlowRendererKey, FlowRendererRegistry } from "@flowgram.ai/renderer";
import type { CanvasSchema, NodeTypeMetadata } from "../types";
import { toEditorCanvasSchema, toFlowgramWorkflowJSON } from "./workflow-json-bridge";
import { WorkflowNodeRender } from "../node-render/node-render";
import { buildNodePortsRuntime, validateConnectionCandidate } from "../editor/connection-rules";
import { FlowgramSelectionBridgeContext } from "./selection-bridge";

interface WorkflowLoaderProps {
  canvas: CanvasSchema;
  readonly?: boolean;
  nodeTypesMeta: NodeTypeMetadata[];
  edgeStateByKey?: Record<string, "idle" | "running" | "incomplete" | "success" | "failed" | "skipped">;
  onCanvasChange: (next: CanvasSchema) => void;
  onSelectionChange?: (nodeKeys: string[]) => void;
  onPortClick?: (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => void;
  onDragLineEnd?: (params: onDragLineEndParams) => void;
}

function createCanvasSyncSignature(canvas: CanvasSchema): string {
  const normalizedNodes = [...(canvas.nodes ?? [])]
    .map((node) => ({
      key: node.key,
      type: node.type,
      title: node.title,
      layout: node.layout,
      configs: node.configs,
      inputMappings: node.inputMappings,
      childCanvas: node.childCanvas
    }))
    .sort((left, right) => left.key.localeCompare(right.key));

  const normalizedConnections = [...(canvas.connections ?? [])]
    .map((connection) => ({
      fromNode: connection.fromNode,
      fromPort: connection.fromPort,
      toNode: connection.toNode,
      toPort: connection.toPort,
      condition: connection.condition ?? null
    }))
    .sort((left, right) =>
      `${left.fromNode}:${left.fromPort}->${left.toNode}:${left.toPort}`.localeCompare(
        `${right.fromNode}:${right.fromPort}->${right.toNode}:${right.toPort}`
      )
    );

  return JSON.stringify({
    nodes: normalizedNodes,
    connections: normalizedConnections,
    globals: canvas.globals ?? {},
    viewport: canvas.viewport ?? null
  });
}

function FlowgramRendererRegistrar() {
  const rendererRegistry = useService(FlowRendererRegistry);

  useEffect(() => {
    rendererRegistry.registerReactComponent(FlowRendererKey.NODE_RENDER, WorkflowNodeRender);
  }, [rendererRegistry]);

  return null;
}

function FlowgramExternalCanvasSync(props: { canvas: CanvasSchema; nodeTypesMap: Map<string, NodeTypeMetadata> }) {
  const ctx = useClientContext();
  const lastAppliedSignatureRef = useRef("");

  useEffect(() => {
    const runtimeCanvas = toEditorCanvasSchema(ctx.document.toJSON() as WorkflowJSON, props.canvas);
    const nextSignature = createCanvasSyncSignature(props.canvas);
    const runtimeSignature = createCanvasSyncSignature(runtimeCanvas);

    if (runtimeSignature === nextSignature || lastAppliedSignatureRef.current === nextSignature) {
      lastAppliedSignatureRef.current = "";
      return;
    }

    lastAppliedSignatureRef.current = nextSignature;
    ctx.operation.fromJSON(toFlowgramWorkflowJSON(props.canvas, props.nodeTypesMap));
  }, [ctx, props.canvas, props.nodeTypesMap]);

  return null;
}

export function WorkflowLoader(props: WorkflowLoaderProps) {
  const lastSelectedNodeKeyRef = useRef("");
  const isReady = props.nodeTypesMeta.length > 0;

  const reportNodeSelection = useCallback(
    (nodeKey: string) => {
      if (!nodeKey) {
        return;
      }
      if (lastSelectedNodeKeyRef.current === nodeKey) {
        return;
      }
      lastSelectedNodeKeyRef.current = nodeKey;
      props.onSelectionChange?.([nodeKey]);
    },
    [props.onSelectionChange]
  );

  const reportPortClick = useCallback(
    (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => {
      props.onPortClick?.(params);
    },
    [props.onPortClick]
  );

  const buildEdgeRuntimeKey = (sourceNode: string, sourcePort: string, targetNode: string, targetPort: string) =>
    `${sourceNode}:${sourcePort}->${targetNode}:${targetPort}`;

  const toRuntimePort = (port: WorkflowPortEntity) => ({
    key: String(port.portID ?? (port.portType === "input" ? "input" : "output")),
    name: String(port.portID ?? (port.portType === "input" ? "input" : "output")),
    direction: port.portType,
    dataType: "any",
    isRequired: false,
    maxConnections: 99
  });

  const nodeTypesMap = useMemo(
    () => new Map<string, NodeTypeMetadata>(props.nodeTypesMeta.map((item) => [String(item.key), item])),
    [props.nodeTypesMeta]
  );

  const initialData = useMemo<WorkflowJSON>(() => toFlowgramWorkflowJSON(props.canvas, nodeTypesMap), [props.canvas, nodeTypesMap]);
  const nodeRegistries = useMemo<WorkflowNodeRegistry[]>(
    () =>
      props.nodeTypesMeta.map((meta) => ({
        type: String(meta.key),
        render: WorkflowNodeRender,
        meta: {
          draggable: true,
          selectable: true,
          size: { width: 360, height: 160 },
          useDynamicPort: String(meta.key) === "Selector",
          defaultPorts:
            meta.ports?.map((port) => ({
              type: port.direction === "Input" || port.direction === 1 ? "input" : "output",
              portID: port.key
            })) ?? []
        }
      })),
    [props.nodeTypesMeta]
  );

  if (!isReady) {
    return null;
  }

  return (
    <FlowgramSelectionBridgeContext.Provider value={{ selectNode: reportNodeSelection, reportPortClick }}>
      <FreeLayoutEditorProvider
        initialData={initialData}
        nodeRegistries={nodeRegistries}
        readonly={props.readonly}
        selectBox={{}}
        history={{ disableShortcuts: false }}
        setLineRenderType={() => 0}
        setLineClassName={(_ctx, line) => {
        const lineData = line.lineData as { processing?: boolean } | undefined;
        const fromNode = line.from?.id ?? "";
        const fromPort = String(line.fromPort?.portID ?? "output");
        const toNode = line.to?.id ?? "";
        const toPort = String(line.toPort?.portID ?? "input");
        const edgeState = props.edgeStateByKey?.[buildEdgeRuntimeKey(fromNode, fromPort, toNode, toPort)] ?? "idle";

        if (lineData?.processing || line.flowing || edgeState === "running") {
          return "wf-react-edge-path-running";
        }
        if (edgeState === "incomplete") {
          return "wf-react-edge-path-incomplete";
        }
        if (edgeState === "success") {
          return "wf-react-edge-path-success";
        }
        if (edgeState === "failed") {
          return "wf-react-edge-path-failed";
        }
        if (edgeState === "skipped") {
          return "wf-react-edge-path-skipped";
        }
        return undefined;
      }}
        canAddLine={(ctx, fromPort, toPort, lines) => {
        const fromNodeType = String(fromPort.node.flowNodeType);
        const toNodeType = String(toPort.node.flowNodeType);
        const fromMeta = nodeTypesMap.get(fromNodeType);
        const toMeta = nodeTypesMap.get(toNodeType);
        const fromPorts = buildNodePortsRuntime(fromMeta);
        const toPorts = buildNodePortsRuntime(toMeta);
        const runtimeFromOutputs = (fromPort.node.ports.outputPorts ?? []).map(toRuntimePort);
        const runtimeToInputs = (toPort.node.ports.inputPorts ?? []).map(toRuntimePort);
        const existing = lines.getAllAvailableLines().map((item) => ({
          id: item.id,
          fromNode: item.from?.id ?? "",
          fromPort: String(item.fromPort?.portID ?? "output"),
          toNode: item.to?.id ?? "",
          toPort: String(item.toPort?.portID ?? "input"),
          condition: ((item.lineData ?? {}) as { condition?: string | null }).condition ?? null
        }));
        const validation = validateConnectionCandidate(
          {
            fromNode: fromPort.node.id,
            fromPort: String(fromPort.portID ?? "output"),
            toNode: toPort.node.id,
            toPort: String(toPort.portID ?? "input")
          },
          existing,
          runtimeFromOutputs.length > 0 ? runtimeFromOutputs : fromPorts.outputs,
          runtimeToInputs.length > 0 ? runtimeToInputs : toPorts.inputs
        );

        return validation.ok;
      }}
        onContentChange={(ctx, _event: WorkflowContentChangeEvent) => {
          const latest = ctx.document.toJSON() as WorkflowJSON;
          props.onCanvasChange(toEditorCanvasSchema(latest, props.canvas));
        }}
        onDragLineEnd={async (_ctx, params) => {
          props.onDragLineEnd?.(params);
        }}
      >
        <FlowgramRendererRegistrar />
        <FlowgramExternalCanvasSync canvas={props.canvas} nodeTypesMap={nodeTypesMap} />
        <EditorRenderer />
      </FreeLayoutEditorProvider>
    </FlowgramSelectionBridgeContext.Provider>
  );
}
