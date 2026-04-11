import type { NodeTypeMetadata, WorkflowNodePortMetadata } from "../types";

export type PortDirection = "input" | "output";

export interface PortRuntime {
  key: string;
  name: string;
  direction: PortDirection;
  dataType: string;
  isRequired: boolean;
  maxConnections: number;
}

export interface NodePortsRuntime {
  inputs: PortRuntime[];
  outputs: PortRuntime[];
}

export interface ConnectionRuntime {
  id: string;
  fromNode: string;
  fromPort: string;
  toNode: string;
  toPort: string;
  condition: string | null;
}

export interface ConnectionCandidate {
  fromNode: string;
  fromPort: string;
  toNode: string;
  toPort: string;
}

export interface ConnectionValidationResult {
  ok: boolean;
  code:
    | "OK"
    | "MISSING_PORT"
    | "INVALID_DIRECTION"
    | "SELF_LOOP"
    | "DUPLICATE"
    | "MAX_CONNECTIONS"
    | "TYPE_MISMATCH";
  message: string;
}

const COMPATIBLE_TYPE_RULES: Record<string, string[]> = {
  any: ["string", "number", "boolean", "object", "array", "json", "unknown", "any"],
  json: ["object", "array", "string", "number", "boolean", "json", "unknown", "any"],
  object: ["json", "object", "unknown", "any"],
  array: ["json", "array", "unknown", "any"],
  string: ["string", "unknown", "any"],
  number: ["number", "unknown", "any"],
  boolean: ["boolean", "unknown", "any"],
  unknown: ["string", "number", "boolean", "object", "array", "json", "unknown", "any"]
};

function normalizeDataType(value: string | undefined): string {
  if (!value) {
    return "any";
  }
  const normalized = value.trim().toLowerCase();
  if (!normalized) {
    return "any";
  }
  if (normalized === "int" || normalized === "float" || normalized === "double" || normalized === "decimal") {
    return "number";
  }
  if (normalized === "bool") {
    return "boolean";
  }
  if (normalized === "dict" || normalized === "map") {
    return "object";
  }
  return normalized;
}

function normalizeDirection(direction: WorkflowNodePortMetadata["direction"] | undefined): PortDirection {
  if (direction === "Input" || direction === 1) {
    return "input";
  }
  return "output";
}

function toPortRuntime(port: WorkflowNodePortMetadata, index: number): PortRuntime {
  const key = port.key?.trim() || `port_${index}`;
  return {
    key,
    name: port.name?.trim() || key,
    direction: normalizeDirection(port.direction),
    dataType: normalizeDataType(port.dataType),
    isRequired: Boolean(port.isRequired),
    maxConnections: port.maxConnections > 0 ? port.maxConnections : 1
  };
}

export function buildNodePortsRuntime(meta: NodeTypeMetadata | undefined): NodePortsRuntime {
  if (!meta?.ports || meta.ports.length === 0) {
    return {
      inputs: [
        {
          key: "input",
          name: "input",
          direction: "input",
          dataType: "any",
          isRequired: false,
          maxConnections: 1
        }
      ],
      outputs: [
        {
          key: "output",
          name: "output",
          direction: "output",
          dataType: "any",
          isRequired: false,
          maxConnections: 99
        }
      ]
    };
  }
  const ports = meta.ports.map(toPortRuntime);
  const inputs = ports.filter((port) => port.direction === "input");
  const outputs = ports.filter((port) => port.direction === "output");
  return {
    inputs: inputs.length > 0 ? inputs : [{ key: "input", name: "input", direction: "input", dataType: "any", isRequired: false, maxConnections: 1 }],
    outputs: outputs.length > 0 ? outputs : [{ key: "output", name: "output", direction: "output", dataType: "any", isRequired: false, maxConnections: 99 }]
  };
}

export function resolveDefaultPortKey(ports: PortRuntime[], fallback: string): string {
  return ports[0]?.key ?? fallback;
}

function isTypeCompatible(fromType: string, toType: string): boolean {
  const normalizedFrom = normalizeDataType(fromType);
  const normalizedTo = normalizeDataType(toType);
  if (normalizedFrom === normalizedTo) {
    return true;
  }
  const allow = COMPATIBLE_TYPE_RULES[normalizedFrom];
  if (!allow) {
    return normalizedFrom === "any" || normalizedTo === "any";
  }
  return allow.includes(normalizedTo);
}

function findPort(ports: PortRuntime[], key: string): PortRuntime | undefined {
  return ports.find((port) => port.key === key);
}

export function validateConnectionCandidate(
  candidate: ConnectionCandidate,
  existing: ConnectionRuntime[],
  fromPorts: PortRuntime[],
  toPorts: PortRuntime[]
): ConnectionValidationResult {
  const source = findPort(fromPorts, candidate.fromPort);
  const target = findPort(toPorts, candidate.toPort);
  if (!source || !target) {
    return { ok: false, code: "MISSING_PORT", message: "连接端口不存在，请刷新节点元数据后重试。" };
  }
  if (source.direction !== "output" || target.direction !== "input") {
    return { ok: false, code: "INVALID_DIRECTION", message: "仅允许从输出端口连接到输入端口。" };
  }
  if (candidate.fromNode === candidate.toNode) {
    return { ok: false, code: "SELF_LOOP", message: "当前配置禁止节点自环连接。" };
  }
  const duplicate = existing.some(
    (item) =>
      item.fromNode === candidate.fromNode &&
      item.fromPort === candidate.fromPort &&
      item.toNode === candidate.toNode &&
      item.toPort === candidate.toPort
  );
  if (duplicate) {
    return { ok: false, code: "DUPLICATE", message: "相同端点之间已存在连接。" };
  }
  const outgoingCount = existing.filter((item) => item.fromNode === candidate.fromNode && item.fromPort === candidate.fromPort).length;
  const incomingCount = existing.filter((item) => item.toNode === candidate.toNode && item.toPort === candidate.toPort).length;
  if (outgoingCount >= source.maxConnections || incomingCount >= target.maxConnections) {
    return { ok: false, code: "MAX_CONNECTIONS", message: "端口连接数量已达到上限。" };
  }
  if (!isTypeCompatible(source.dataType, target.dataType)) {
    return {
      ok: false,
      code: "TYPE_MISMATCH",
      message: `端口类型不兼容：${source.dataType} -> ${target.dataType}。`
    };
  }
  return { ok: true, code: "OK", message: "连接合法" };
}
