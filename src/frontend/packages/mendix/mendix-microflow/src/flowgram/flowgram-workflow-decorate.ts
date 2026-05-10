import type { MicroflowTraceFrame } from "../debug/trace-types";
import { deriveNodeInlineConfig } from "../node-inline";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { MICROFLOW_ROOT_COLLECTION_ID, flattenFlowGramWorkflowForPersistence } from "./flowgram-native-schema";
import { flowGramPortsForObjectKind } from "./adapters/flowgram-port-factory";
import type {
  FlowGramMicroflowEdgeData,
  FlowGramMicroflowNodeData,
  MicroflowNodeViewMode,
} from "./FlowGramMicroflowTypes";
import { deriveEdgeRuntimeStateByFlowId } from "./runtime-edge-state";
import { normalizeMicroflowDesignEdges } from "./flowgram-design-edge-semantics";
import { getMendixMicroflowNodeSize } from "./flowgram-node-geometry";
import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

function edgeId(edge: MicroflowWorkflowEdgeJSON | WorkflowEdgeJSON): string | undefined {
  const data = (edge as MicroflowWorkflowEdgeJSON).data as Partial<FlowGramMicroflowEdgeData> | undefined;
  return data?.flowId ?? (edge as { id?: string }).id;
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

function normalizeWorkflow(workflow: WorkflowJSON): WorkflowJSON {
  const flatWorkflow = flattenFlowGramWorkflowForPersistence(workflow);
  const nodes = ((flatWorkflow.nodes ?? []) as MicroflowWorkflowNodeJSON[]).map(ensureNodeData);
  return {
    ...flatWorkflow,
    nodes: nodes as WorkflowJSON["nodes"],
    edges: normalizeMicroflowDesignEdges({ ...flatWorkflow, nodes } as WorkflowJSON).map(ensureEdgeData) as WorkflowJSON["edges"],
  };
}

function nodeViewModeAliases(nodeId: string, objectId?: string): string[] {
  const aliases = new Set<string>();
  for (const value of [nodeId, objectId]) {
    if (!value) {
      continue;
    }
    aliases.add(value);
    if (value.startsWith("node-")) {
      aliases.add(value.slice("node-".length));
    } else {
      aliases.add(`node-${value}`);
    }
  }
  return [...aliases];
}

function resolveNodeViewMode(
  nodeId: string,
  data: FlowGramMicroflowNodeData,
  nodeViewModes?: Record<string, MicroflowNodeViewMode>,
): MicroflowNodeViewMode | undefined {
  for (const alias of nodeViewModeAliases(nodeId, data.objectId)) {
    const mode = nodeViewModes?.[alias];
    if (mode) {
      return mode;
    }
  }
  return undefined;
}

export function runtimeStateFromTraceStatus(status: MicroflowTraceFrame["status"] | undefined): FlowGramMicroflowNodeData["runtimeState"] {
  if (!status) {
    return "idle";
  }
  if (status === "success") {
    return "success";
  }
  if (status === "running") {
    return "running";
  }
  if (status === "failed") {
    return "failed";
  }
  if (status === "skipped") {
    return "skipped";
  }
  if (status === "unsupported") {
    return "unsupported";
  }
  return "visited";
}

export function decorateWorkflow(input: {
  schema: MicroflowDesignSchema;
  validationIssues: MicroflowValidationIssue[];
  runtimeTrace: MicroflowTraceFrame[];
  nodeViewModes?: Record<string, MicroflowNodeViewMode>;
}): WorkflowJSON {
  const normalized = normalizeWorkflow(input.schema.workflow as WorkflowJSON);
  const runtimeFrameByObjectId = new Map<string, MicroflowTraceFrame>();
  for (const frame of input.runtimeTrace) {
    if (frame.objectId) {
      runtimeFrameByObjectId.set(frame.objectId, frame);
    }
  }

  const edgeRuntimeByFlowId = deriveEdgeRuntimeStateByFlowId(input.runtimeTrace);

  const nodes = (normalized.nodes ?? []) as MicroflowWorkflowNodeJSON[];
  const edges = (normalized.edges ?? []) as MicroflowWorkflowEdgeJSON[];
  const nodeDataById = new Map<string, FlowGramMicroflowNodeData>(
    nodes.map(node => [node.id, (node.data ?? {}) as unknown as FlowGramMicroflowNodeData]),
  );
  return {
    ...normalized,
    nodes: nodes.map(node => {
      const data = (node.data ?? {}) as unknown as FlowGramMicroflowNodeData;
      const frame = runtimeFrameByObjectId.get(node.id) ?? runtimeFrameByObjectId.get(data.objectId);
      const nodeIssues = input.validationIssues.filter(item => item.objectId === node.id || item.nodeId === node.id);
      const validationState: FlowGramMicroflowNodeData["validationState"] =
        nodeIssues.some(item => item.severity === "error")
          ? "error"
          : nodeIssues.some(item => item.severity === "warning")
            ? "warning"
            : "valid";
      const runtimeState = runtimeStateFromTraceStatus(frame?.status);
      const viewMode = resolveNodeViewMode(node.id, data, input.nodeViewModes);
      const expanded = viewMode === "expanded" || viewMode === "editing" || viewMode === "inspectingError" || viewMode === "inspectingRuntime";
      const inlineConfig = deriveNodeInlineConfig({
        node,
        schema: input.schema,
        runtimeFrame: frame,
        issues: nodeIssues,
        viewMode,
      });
      return {
        ...node,
        meta: {
          ...node.meta,
          size: getMendixMicroflowNodeSize(data.objectKind, { expanded }),
        },
        data: {
          ...data,
          validationState,
          issueCount: nodeIssues.length,
          runtimeState,
          runtimeErrorCode: frame?.error?.code,
          runtimeErrorMessage: frame?.error?.message,
          inlineConfig,
        },
      };
    }) as WorkflowJSON["nodes"],
    edges: edges.map((edge, index) => {
      const next = ensureEdgeData(edge, index);
      const data = (next.data ?? {}) as unknown as FlowGramMicroflowEdgeData;
      const flowId = data.flowId ?? next.id;
      const sourceNode = nodeDataById.get(String(next.sourceNodeID ?? ""));
      const targetNode = nodeDataById.get(String(next.targetNodeID ?? ""));
      const issue = input.validationIssues.find(item => item.flowId === flowId);
      const validationState: FlowGramMicroflowEdgeData["validationState"] =
        issue?.severity === "error" ? "error" : issue?.severity === "warning" ? "warning" : "valid";
      const runtimeState = edgeRuntimeByFlowId.get(String(flowId)) ?? data.runtimeState ?? "idle";
      return {
        ...next,
        data: {
          ...data,
          sourceNodeId: String(next.sourceNodeID ?? ""),
          sourceObjectKind: sourceNode?.objectKind,
          sourceActionKind: sourceNode?.actionKind,
          sourcePortId: String(next.sourcePortID ?? ""),
          targetNodeId: String(next.targetNodeID ?? ""),
          targetObjectKind: targetNode?.objectKind,
          targetActionKind: targetNode?.actionKind,
          targetPortId: String(next.targetPortID ?? ""),
          validationState,
          runtimeState,
        },
      };
    }) as WorkflowJSON["edges"],
  };
}
