import type { MicroflowAnnotationFlow, MicroflowFlow, MicroflowLine, MicroflowSchema, MicroflowSequenceFlow } from "../types";
import { collectFlowsRecursive } from "./object-utils";

export function isSequenceFlow(flow: MicroflowFlow): flow is MicroflowSequenceFlow {
  return flow.kind === "sequence";
}

export function isAnnotationFlow(flow: MicroflowFlow): flow is MicroflowAnnotationFlow {
  return flow.kind === "annotation";
}

export function findFlowById(flows: MicroflowFlow[], flowId: string): MicroflowFlow | undefined {
  return flows.find(flow => flow.id === flowId);
}

export function findFlowByIdRecursive(schema: MicroflowSchema, flowId: string): MicroflowFlow | undefined {
  return collectFlowsRecursive(schema).find(flow => flow.id === flowId);
}

export function createDefaultLine(): MicroflowLine {
  return {
    kind: "orthogonal",
    points: [],
    routing: { mode: "auto", bendPoints: [] },
    style: { strokeType: "solid", strokeWidth: 2, arrow: "target" }
  };
}
