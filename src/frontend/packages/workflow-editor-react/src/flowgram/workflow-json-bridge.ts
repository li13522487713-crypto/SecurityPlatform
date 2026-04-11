import type { NodeTypeMetadata, WorkflowNodePortMetadata } from "../types";
import type { CanvasSchema, ConnectionSchema, NodeSchema } from "../types/workflow-v2";
import type { WorkflowJSON } from "@flowgram.ai/free-layout-editor";

type PortDirection = "input" | "output";

interface FlowgramPort {
  type: PortDirection;
  portID?: string | number;
}

function normalizeDirection(direction: WorkflowNodePortMetadata["direction"]): PortDirection {
  return direction === "Input" || direction === 1 ? "input" : "output";
}

function resolveDefaultPorts(node: NodeSchema, nodeTypesMap: Map<string, NodeTypeMetadata>): FlowgramPort[] {
  const meta = nodeTypesMap.get(node.type);
  const ports = meta?.ports ?? [];
  if (ports.length === 0) {
    return [
      { type: "input", portID: "input" },
      { type: "output", portID: "output" }
    ];
  }

  return ports.map((port) => ({
    type: normalizeDirection(port.direction),
    portID: port.key
  }));
}

export function toFlowgramWorkflowJSON(canvas: CanvasSchema, nodeTypesMap: Map<string, NodeTypeMetadata>): WorkflowJSON {
  return {
    nodes: (canvas.nodes ?? []).map((node) => ({
      // 将运行态挂载到 data，供 WorkflowNodeRender 即时消费。
      ...(() => {
        const debugMeta = (node.debugMeta ?? {}) as Record<string, unknown>;
        return {
          id: node.key,
          type: node.type,
          data: {
            title: node.title,
            configs: node.configs,
            inputMappings: node.inputMappings,
            version: node.version,
            debugMeta: node.debugMeta,
            executionState: typeof debugMeta.executionState === "string" ? debugMeta.executionState : undefined,
            executionHint: typeof debugMeta.executionHint === "string" ? debugMeta.executionHint : undefined
          }
        };
      })(),
      meta: {
        position: {
          x: node.layout?.x ?? 0,
          y: node.layout?.y ?? 0
        },
        defaultPorts:
          node.ports?.map((port) => ({ type: port.direction, portID: port.key })) ?? resolveDefaultPorts(node, nodeTypesMap),
        useDynamicPort: node.type === "Selector"
      }
    })),
    edges: (canvas.connections ?? []).map((line) => ({
      sourceNodeID: line.fromNode,
      targetNodeID: line.toNode,
      sourcePortID: line.fromPort,
      targetPortID: line.toPort,
      data: {
        condition: line.condition
      }
    }))
  };
}

export function toEditorCanvasSchema(json: WorkflowJSON, previous: CanvasSchema): CanvasSchema {
  const prevNodeMap = new Map(previous.nodes.map((node) => [node.key, node]));
  const nodes: NodeSchema[] = (json.nodes ?? []).map((node) => {
    const prevNode = prevNodeMap.get(node.id);
    const nodeData = (node.data ?? {}) as Record<string, unknown>;
    const title = typeof nodeData.title === "string" ? nodeData.title : prevNode?.title ?? String(node.type);
    const configs = (nodeData.configs as Record<string, unknown>) ?? prevNode?.configs ?? {};
    const inputMappings = (nodeData.inputMappings as Record<string, string>) ?? prevNode?.inputMappings ?? {};
    const position = (node.meta?.position ?? {}) as { x?: number; y?: number };

    return {
      key: node.id,
      type: String(node.type) as NodeSchema["type"],
      title,
      layout: {
        x: typeof position.x === "number" ? position.x : prevNode?.layout?.x ?? 0,
        y: typeof position.y === "number" ? position.y : prevNode?.layout?.y ?? 0,
        width: prevNode?.layout?.width ?? 360,
        height: prevNode?.layout?.height ?? 160
      },
      configs,
      inputMappings,
      childCanvas: prevNode?.childCanvas,
      inputTypes: prevNode?.inputTypes,
      outputTypes: prevNode?.outputTypes,
      inputSources: prevNode?.inputSources,
      outputSources: prevNode?.outputSources,
      ports: prevNode?.ports,
      version: typeof nodeData.version === "string" ? nodeData.version : prevNode?.version,
      debugMeta:
        (nodeData.debugMeta as Record<string, unknown> | undefined) ??
        (() => {
          const nextMeta = { ...(prevNode?.debugMeta ?? {}) };
          if (typeof nodeData.executionState === "string") {
            nextMeta.executionState = nodeData.executionState;
          }
          if (typeof nodeData.executionHint === "string") {
            nextMeta.executionHint = nodeData.executionHint;
          }
          return Object.keys(nextMeta).length > 0 ? nextMeta : undefined;
        })()
    };
  });

  const connections: ConnectionSchema[] = (json.edges ?? []).map((edge) => ({
    fromNode: edge.sourceNodeID,
    fromPort: String(edge.sourcePortID ?? "output"),
    toNode: edge.targetNodeID,
    toPort: String(edge.targetPortID ?? "input"),
    condition: (edge.data as { condition?: string | null } | undefined)?.condition ?? null
  }));

  return {
    nodes,
    connections,
    schemaVersion: previous.schemaVersion,
    viewport: previous.viewport
  };
}
