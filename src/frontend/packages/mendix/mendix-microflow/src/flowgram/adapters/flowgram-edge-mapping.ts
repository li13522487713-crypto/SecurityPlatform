import type { WorkflowEdgeJSON } from "@flowgram-adapter/free-layout-editor";

import { toEditorGraph } from "../../adapters";
import { getDefaultSourcePortForEdgeKind, getDefaultTargetPortForEdgeKind, parsePortId } from "../../schema/utils/port-utils";
import type { MicroflowCaseValue, MicroflowEditorPort, MicroflowFlow, MicroflowSchema } from "../../schema";
import type { FlowGramMicroflowEdgeData } from "../FlowGramMicroflowTypes";
import { createMicroflowFlowFromPorts } from "./flowgram-edge-factory";

export type FlowGramEdgeLike = Pick<WorkflowEdgeJSON, "sourceNodeID" | "targetNodeID" | "sourcePortID" | "targetPortID"> & {
  id?: string;
  data?: Partial<FlowGramMicroflowEdgeData>;
};

function portsById(schema: MicroflowSchema): Map<string, MicroflowEditorPort> {
  return new Map(toEditorGraph(schema).nodes.flatMap(node => node.ports).map(port => [port.id, port]));
}

function flowEdgeKind(flow: MicroflowFlow): NonNullable<MicroflowFlow["editor"]["edgeKind"]> {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  return flow.isErrorHandler ? "errorHandler" : flow.editor.edgeKind;
}

export function getSourceHandleFromConnectionIndex(
  objectId: string,
  edgeKind: NonNullable<MicroflowFlow["editor"]["edgeKind"]>,
  connectionIndex: number,
): string {
  return `${objectId}:${getDefaultSourcePortForEdgeKind(edgeKind)}:${connectionIndex}`;
}

export function getTargetHandleFromConnectionIndex(
  objectId: string,
  edgeKind: NonNullable<MicroflowFlow["editor"]["edgeKind"]>,
  connectionIndex: number,
): string {
  return `${objectId}:${getDefaultTargetPortForEdgeKind(edgeKind)}:${connectionIndex}`;
}

export function getConnectionIndexFromSourceHandle(sourceHandle?: string | number): number {
  return sourceHandle === undefined ? 0 : parsePortId(String(sourceHandle))?.connectionIndex ?? 0;
}

export function getConnectionIndexFromTargetHandle(targetHandle?: string | number): number {
  return targetHandle === undefined ? 0 : parsePortId(String(targetHandle))?.connectionIndex ?? 0;
}

export function mapFlowGramEdgeToMicroflowFlow(schema: MicroflowSchema, edge: FlowGramEdgeLike): MicroflowFlow | undefined {
  if (!edge.sourceNodeID || !edge.targetNodeID || edge.sourceNodeID === edge.targetNodeID) {
    return undefined;
  }
  const ports = portsById(schema);
  const sourcePort = edge.sourcePortID === undefined ? undefined : ports.get(String(edge.sourcePortID));
  const targetPort = edge.targetPortID === undefined ? undefined : ports.get(String(edge.targetPortID));
  if (!sourcePort || !targetPort) {
    return undefined;
  }
  const caseValues = edge.data?.caseValues as MicroflowCaseValue[] | undefined;
  return createMicroflowFlowFromPorts(schema, sourcePort, targetPort, {
    caseValues,
    label: edge.data?.label,
  });
}

export function mapMicroflowFlowToFlowGramEdge(schema: MicroflowSchema, flow: MicroflowFlow): FlowGramEdgeLike {
  const graphEdge = toEditorGraph(schema).edges.find(edge => edge.flowId === flow.id);
  if (graphEdge) {
    return {
      id: graphEdge.flowId,
      sourceNodeID: graphEdge.sourceObjectId ?? graphEdge.sourceNodeId ?? flow.originObjectId,
      targetNodeID: graphEdge.targetObjectId ?? graphEdge.targetNodeId ?? flow.destinationObjectId,
      sourcePortID: graphEdge.sourcePortId,
      targetPortID: graphEdge.targetPortId,
      data: {
        flowId: flow.id,
        flowKind: flow.kind,
        edgeKind: flowEdgeKind(flow),
        isErrorHandler: flow.kind === "sequence" ? flow.isErrorHandler : false,
        caseValues: flow.kind === "sequence" ? flow.caseValues : [],
        label: flow.editor.label,
        description: flow.editor.description,
        branchOrder: flow.kind === "sequence" ? flow.editor.branchOrder : undefined,
        showInExport: flow.kind === "annotation" ? flow.editor.showInExport : undefined,
        validationState: "valid",
      } satisfies FlowGramMicroflowEdgeData,
    };
  }

  const edgeKind = flowEdgeKind(flow);
  return {
    id: flow.id,
    sourceNodeID: flow.originObjectId,
    targetNodeID: flow.destinationObjectId,
    sourcePortID: getSourceHandleFromConnectionIndex(flow.originObjectId, edgeKind, flow.originConnectionIndex ?? 0),
    targetPortID: getTargetHandleFromConnectionIndex(flow.destinationObjectId, edgeKind, flow.destinationConnectionIndex ?? 0),
    data: {
      flowId: flow.id,
      flowKind: flow.kind,
      edgeKind,
      isErrorHandler: flow.kind === "sequence" ? flow.isErrorHandler : false,
      caseValues: flow.kind === "sequence" ? flow.caseValues : [],
      label: flow.editor.label,
      description: flow.editor.description,
      branchOrder: flow.kind === "sequence" ? flow.editor.branchOrder : undefined,
      showInExport: flow.kind === "annotation" ? flow.editor.showInExport : undefined,
      validationState: "valid",
    } satisfies FlowGramMicroflowEdgeData,
  };
}
