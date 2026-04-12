import { useCallback, useMemo, useRef } from "react";
import {
  FreeLayoutEditor,
  type WorkflowJSON,
  type WorkflowContentChangeEvent,
  type WorkflowNodeRegistry,
  type WorkflowPortEntity,
  type onDragLineEndParams
} from "@flowgram.ai/free-layout-editor";
import type { CanvasSchema, NodeTypeMetadata } from "../types";
import { toEditorCanvasSchema, toFlowgramWorkflowJSON } from "./workflow-json-bridge";
import { WorkflowNodeRender } from "../node-render/node-render";
import { buildNodePortsRuntime, validateConnectionCandidate } from "../editor/connection-rules";
import { FlowgramSelectionBridgeContext } from "./selection-bridge";

interface WorkflowLoaderProps {
  canvas: CanvasSchema;
  readonly?: boolean;
  nodeTypesMeta: NodeTypeMetadata[];
  edgeStateByKey?: Record<string, "idle" | "running" | "success" | "failed" | "skipped">;
  onCanvasChange: (next: CanvasSchema) => void;
  onSelectionChange?: (nodeKeys: string[]) => void;
  onPortClick?: (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => void;
  onDragLineEnd?: (params: onDragLineEndParams) => void;
}

export function WorkflowLoader(props: WorkflowLoaderProps) {
  const selectionMapRef = useRef<Record<string, boolean>>({});

  const reportNodeSelection = useCallback(
    (nodeKey: string, selected: boolean) => {
      if (!nodeKey) {
        return;
      }
      if (selectionMapRef.current[nodeKey] === selected) {
        return;
      }
      selectionMapRef.current = {
        ...selectionMapRef.current,
        [nodeKey]: selected
      };
      if (props.onSelectionChange) {
        const selectedNodeKeys = Object.entries(selectionMapRef.current)
          .filter(([, value]) => value)
          .map(([key]) => key);
        props.onSelectionChange(selectedNodeKeys);
      }
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

  return (
    <FlowgramSelectionBridgeContext.Provider value={{ reportNodeSelection, reportPortClick }}>
      <FreeLayoutEditor
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
      />
    </FlowgramSelectionBridgeContext.Provider>
  );
}
