import type { MicroflowObjectKind, MicroflowPoint, MicroflowSize } from "../schema";

export const MICROFLOW_EVENT_NODE_SIZE: MicroflowSize = { width: 80, height: 28 };
export const MICROFLOW_ACTIVITY_NODE_SIZE: MicroflowSize = { width: 56, height: 56 };
export const MICROFLOW_DECISION_NODE_SIZE: MicroflowSize = { width: 40, height: 40 };
export const MICROFLOW_MERGE_NODE_SIZE: MicroflowSize = { width: 40, height: 40 };
export const MICROFLOW_LOOP_NODE_SIZE: MicroflowSize = { width: 320, height: 190 };
export const MICROFLOW_PARAMETER_NODE_SIZE: MicroflowSize = { width: 118, height: 56 };
export const MICROFLOW_ANNOTATION_NODE_SIZE: MicroflowSize = { width: 210, height: 96 };
export const MICROFLOW_EXPANDED_NODE_SIZE: MicroflowSize = { width: 420, height: 240 };

const eventKinds = new Set<string>([
  "startEvent",
  "endEvent",
  "errorEvent",
  "breakEvent",
  "continueEvent",
]);

const decisionKinds = new Set<string>([
  "exclusiveSplit",
  "inheritanceSplit",
  "parallelGateway",
  "inclusiveGateway",
]);

export function getMendixMicroflowNodeSize(
  kind: MicroflowObjectKind | string | undefined,
  options: { expanded?: boolean } = {},
): MicroflowSize {
  if (options.expanded) {
    return MICROFLOW_EXPANDED_NODE_SIZE;
  }
  if (kind === "loopedActivity") {
    return MICROFLOW_LOOP_NODE_SIZE;
  }
  if (kind === "annotation") {
    return MICROFLOW_ANNOTATION_NODE_SIZE;
  }
  if (kind === "parameterObject") {
    return MICROFLOW_PARAMETER_NODE_SIZE;
  }
  if (kind === "exclusiveMerge") {
    return MICROFLOW_MERGE_NODE_SIZE;
  }
  if (kind && eventKinds.has(kind)) {
    return MICROFLOW_EVENT_NODE_SIZE;
  }
  if (kind && decisionKinds.has(kind)) {
    return MICROFLOW_DECISION_NODE_SIZE;
  }
  return MICROFLOW_ACTIVITY_NODE_SIZE;
}

export function getMendixMicroflowDropOffset(kind: MicroflowObjectKind | string | undefined): MicroflowPoint {
  const size = getMendixMicroflowNodeSize(kind);
  return {
    x: size.width / 2,
    y: size.height / 2,
  };
}

export function isMendixMicroflowEventNode(kind: MicroflowObjectKind | string | undefined): boolean {
  return Boolean(kind && eventKinds.has(kind));
}

export function isMendixMicroflowDecisionNode(kind: MicroflowObjectKind | string | undefined): boolean {
  return Boolean(kind && decisionKinds.has(kind));
}
