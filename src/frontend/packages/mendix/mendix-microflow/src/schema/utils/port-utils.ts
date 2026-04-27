import type {
  MicroflowEdgeKind,
  MicroflowEditorPort,
  MicroflowObject,
  MicroflowPort,
  MicroflowPortCardinality,
  MicroflowPortDirection,
  MicroflowPortKind,
  MicroflowSchema,
} from "../types";
import { findObjectWithCollection } from "./object-utils";

export type MicroflowPortSide = "top" | "right" | "bottom" | "left";

export interface MicroflowParsedPortId {
  objectId: string;
  portKind: MicroflowPortKind;
  connectionIndex: number;
}

export interface MicroflowResolvedPort {
  id: string;
  objectId: string;
  kind: MicroflowPortKind;
  direction: MicroflowPortDirection;
  connectionIndex: number;
  side: MicroflowPortSide;
  label?: string;
  cardinality: MicroflowPortCardinality;
  collectionId?: string;
}

export function createPortId(objectId: string, portKind: MicroflowPortKind, connectionIndex: number): string {
  return `${objectId}:${portKind}:${connectionIndex}`;
}

export function parsePortId(portId: string): MicroflowParsedPortId | null {
  const parts = portId.split(":");
  if (parts.length === 3) {
    const [objectId, portKind, index] = parts;
    const connectionIndex = Number(index);
    return Number.isFinite(connectionIndex)
      ? { objectId, portKind: portKind as MicroflowPortKind, connectionIndex }
      : null;
  }
  if (parts.length === 2) {
    const [objectId, legacyKind] = parts;
    return { objectId, portKind: legacyKindToPortKind(legacyKind), connectionIndex: legacyConnectionIndex(legacyKind) };
  }
  return null;
}

export function getConnectionIndexFromPortId(portId?: string): number {
  return portId ? parsePortId(portId)?.connectionIndex ?? 0 : 0;
}

export function getPortKindFromPortId(portId?: string): MicroflowPortKind | undefined {
  return portId ? parsePortId(portId)?.portKind : undefined;
}

export function getPortDirectionFromPortId(portId?: string): MicroflowPortDirection | undefined {
  const kind = getPortKindFromPortId(portId);
  if (!kind) {
    return undefined;
  }
  return ["sequenceIn", "loopIn", "loopBodyOut"].includes(kind) ? "input" : "output";
}

function legacyKindToPortKind(value: string): MicroflowPortKind {
  if (value === "in") {
    return "sequenceIn";
  }
  if (value === "out") {
    return "sequenceOut";
  }
  if (value === "true" || value === "false" || value === "case") {
    return "decisionOut";
  }
  if (value === "objectType") {
    return "objectTypeOut";
  }
  if (value === "error") {
    return "errorOut";
  }
  if (value === "note") {
    return "annotation";
  }
  if (value === "bodyIn") {
    return "loopBodyIn";
  }
  if (value === "bodyOut") {
    return "loopBodyOut";
  }
  return value as MicroflowPortKind;
}

function legacyConnectionIndex(value: string): number {
  if (value === "out" || value === "in") {
    return 0;
  }
  if (value === "true" || value === "objectType") {
    return 1;
  }
  if (value === "false") {
    return 2;
  }
  if (value === "error") {
    return 99;
  }
  return 0;
}

function portSide(kind: MicroflowPortKind, direction: MicroflowPortDirection): MicroflowPortSide {
  if (kind === "errorOut") {
    return "bottom";
  }
  if (direction === "input") {
    return "left";
  }
  if (kind === "annotation") {
    return "top";
  }
  return "right";
}

export function portsForObject(object: MicroflowObject): MicroflowPort[] {
  const input: MicroflowPort = { id: "in", label: "In", direction: "input", kind: "sequenceIn", cardinality: "one", edgeTypes: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"] };
  const output: MicroflowPort = { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one", edgeTypes: ["sequence"] };
  const error: MicroflowPort = { id: "error", label: "Error", direction: "output", kind: "errorOut", cardinality: "zeroOrOne", edgeTypes: ["errorHandler"] };
  const loopBodyIn: MicroflowPort = { id: "bodyIn", label: "Body In", direction: "output", kind: "loopBodyIn", cardinality: "one", edgeTypes: ["sequence"] };
  const loopBodyOut: MicroflowPort = { id: "bodyOut", label: "Body Out", direction: "input", kind: "loopBodyOut", cardinality: "zeroOrMore", edgeTypes: ["sequence"] };
  if (object.kind === "startEvent") {
    return [output];
  }
  if (object.kind === "endEvent" || object.kind === "errorEvent" || object.kind === "breakEvent" || object.kind === "continueEvent") {
    return [input];
  }
  if (object.kind === "exclusiveSplit") {
    const decisionOutputs = object.splitCondition.kind === "expression" && object.splitCondition.resultType === "boolean"
      ? [
          { id: "true", label: "true", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] } satisfies MicroflowPort,
          { id: "false", label: "false", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] } satisfies MicroflowPort,
        ]
      : [{ id: "case", label: "Case", direction: "output", kind: "decisionOut", cardinality: "zeroOrMore", edgeTypes: ["decisionCondition"] } satisfies MicroflowPort];
    return [input, ...decisionOutputs, error];
  }
  if (object.kind === "inheritanceSplit") {
    return [input, { id: "objectType", label: "Object type", direction: "output", kind: "objectTypeOut", cardinality: "zeroOrMore", edgeTypes: ["objectTypeCondition"] }, error];
  }
  if (object.kind === "parameterObject") {
    return [];
  }
  if (object.kind === "annotation") {
    return [{ id: "note", label: "Note", direction: "output", kind: "annotation", cardinality: "zeroOrMore", edgeTypes: ["annotation"] }];
  }
  if (object.kind === "loopedActivity") {
    return [input, output, loopBodyIn, loopBodyOut, error];
  }
  return object.kind === "actionActivity" ? [input, output, error] : [input, output];
}

export function resolveObjectPorts(schema: MicroflowSchema, objectId: string, collectionId?: string): MicroflowResolvedPort[] {
  const location = findObjectWithCollection(schema, objectId);
  if (!location || (collectionId && location.collectionId !== collectionId)) {
    return [];
  }
  return portsForObject(location.object).map((port, index) => ({
    id: createPortId(objectId, port.kind, index),
    objectId,
    kind: port.kind,
    direction: port.direction,
    connectionIndex: index,
    side: portSide(port.kind, port.direction),
    label: port.label,
    cardinality: port.cardinality,
    collectionId: location.collectionId,
  }));
}

export function resolvePort(schema: MicroflowSchema, objectId: string, portId: string, collectionId?: string): MicroflowResolvedPort | undefined {
  const parsed = parsePortId(portId);
  return resolveObjectPorts(schema, objectId, collectionId).find(port =>
    port.id === portId ||
    (parsed && port.kind === parsed.portKind && port.connectionIndex === parsed.connectionIndex)
  );
}

export function getDefaultSourcePortForEdgeKind(edgeKind: MicroflowEdgeKind): MicroflowPortKind {
  if (edgeKind === "annotation") {
    return "annotation";
  }
  if (edgeKind === "errorHandler") {
    return "errorOut";
  }
  if (edgeKind === "decisionCondition") {
    return "decisionOut";
  }
  if (edgeKind === "objectTypeCondition") {
    return "objectTypeOut";
  }
  return "sequenceOut";
}

export function getDefaultTargetPortForEdgeKind(edgeKind: MicroflowEdgeKind): MicroflowPortKind {
  return edgeKind === "annotation" ? "annotation" : "sequenceIn";
}

export function toEditorPort(schema: MicroflowSchema, objectId: string, port: MicroflowResolvedPort): MicroflowEditorPort {
  const object = findObjectWithCollection(schema, objectId)?.object;
  const source = object ? portsForObject(object).find(item => item.kind === port.kind) : undefined;
  return {
    id: port.id,
    objectId,
    label: port.label ?? source?.label ?? port.kind,
    direction: port.direction,
    kind: port.kind,
    connectionIndex: port.connectionIndex,
    cardinality: port.cardinality,
    edgeTypes: source?.edgeTypes ?? [],
  };
}
