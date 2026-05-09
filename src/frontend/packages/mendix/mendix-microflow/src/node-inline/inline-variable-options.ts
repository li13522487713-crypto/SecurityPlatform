import type { MicroflowTraceFrame } from "../debug/trace-types";
import type {
  MicroflowAction,
  MicroflowCallMicroflowAction,
  MicroflowCreateVariableAction,
  MicroflowDesignSchema,
  MicroflowRestCallAction,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { normalizeDesignVariables } from "../schema/utils/design-schema-variables";

type InlineVariableOptionMode = "name" | "expression";

interface PushOptionInput {
  source: "input" | "context" | "upstream" | "upstream-direct" | "upstream-indirect" | "loop" | "system" | "error" | "runtime";
  name: string;
  type?: string;
  sourceNode?: string;
  scope?: string;
  readonly?: boolean;
  maybe?: boolean;
  unknown?: boolean;
  preview?: string;
  refCount?: number;
}

function normalizeName(name: string, mode: InlineVariableOptionMode): string {
  const trimmed = name.trim();
  if (!trimmed) {
    return "";
  }
  if (mode === "expression") {
    return trimmed.startsWith("$") ? trimmed : `$${trimmed}`;
  }
  return trimmed.startsWith("$") ? trimmed.slice(1) : trimmed;
}

function normalizeJsonRootName(name: string): string {
  const trimmed = name.trim();
  if (!trimmed) {
    return "";
  }
  if (trimmed.startsWith("$.")) {
    return trimmed;
  }
  if (trimmed.startsWith("$")) {
    return `$.${trimmed.slice(1)}`;
  }
  return `$.${trimmed}`;
}

function parseReferenceCounts(schema: MicroflowDesignSchema): Record<string, number> {
  const counts: Record<string, number> = {};
  const regex = /\$\.?[A-Za-z_][A-Za-z0-9_]*(?:[./][A-Za-z_][A-Za-z0-9_]*)*/g;
  const collectFrom = (value: unknown) => {
    if (!value || typeof value !== "object") {
      return;
    }
    const record = value as Record<string, unknown>;
    const raw = record.raw;
    if (typeof raw === "string" && raw.trim()) {
      for (const match of raw.matchAll(regex)) {
        const token = String(match[0] ?? "");
        const normalized = token.startsWith("$.") ? token.slice(2) : token.slice(1);
        const variableName = normalized.split(/[./]/)[0] ?? normalized;
        if (variableName) {
          counts[variableName] = (counts[variableName] ?? 0) + 1;
        }
      }
    }
    Object.values(record).forEach(collectFrom);
  };
  collectFrom(schema.workflow);
  collectFrom(schema.variables);
  return counts;
}

function pushOption(
  list: Array<{ label: string; value: string }>,
  seen: Set<string>,
  mergedLabels: Map<string, string>,
  input: PushOptionInput,
  mode: InlineVariableOptionMode,
) {
  const value = normalizeName(input.name, mode);
  if (!value) {
    return;
  }
  const metadata = [
    input.type ? `type=${input.type}` : "",
    input.sourceNode ? `sourceNode=${input.sourceNode}` : "",
    input.scope ? `scope=${input.scope}` : "",
    input.readonly ? "readonly=true" : "",
    input.maybe ? "maybe=true" : "",
    input.unknown ? "unknown=true" : "",
    input.preview ? `preview=${input.preview}` : "",
    typeof input.refCount === "number" ? `refCount=${input.refCount}` : "",
  ].filter(Boolean);
  const suffix = metadata.length > 0 ? `|${metadata.join("|")}` : "";
  const baseLabel = `${input.source}::${value}${suffix}`;
  if (seen.has(value)) {
    const existing = mergedLabels.get(value);
    if (existing && !existing.includes(`${input.source}::`)) {
      const merged = `${existing};${baseLabel}`;
      mergedLabels.set(value, merged);
      const item = list.find(entry => entry.value === value);
      if (item) {
        item.label = merged;
      }
    }
    return;
  }
  list.push({ label: baseLabel, value });
  mergedLabels.set(value, baseLabel);
  seen.add(value);
  if (mode === "expression") {
    const jsonRoot = normalizeJsonRootName(input.name);
    if (jsonRoot && !seen.has(jsonRoot)) {
      seen.add(jsonRoot);
      const jsonLabel = `${input.source}::${jsonRoot}${suffix}`;
      list.push({
        label: jsonLabel,
        value: jsonRoot,
      });
      mergedLabels.set(jsonRoot, jsonLabel);
    }
  }
}

function extractActionOutputs(node: MicroflowWorkflowNodeJSON): string[] {
  const nodeData = (node.data ?? {}) as Record<string, unknown>;
  const fallbackOutputs = [
    typeof nodeData.outputVariableName === "string" ? nodeData.outputVariableName : "",
    typeof nodeData.resultVariable === "string" ? nodeData.resultVariable : "",
    typeof nodeData.returnVariableName === "string" ? nodeData.returnVariableName : "",
    typeof nodeData.resultsVariableName === "string" ? nodeData.resultsVariableName : "",
    typeof nodeData.fallbackResultVariable === "string" ? nodeData.fallbackResultVariable : "",
    typeof nodeData.errorVariableName === "string" ? nodeData.errorVariableName : "",
    typeof nodeData.customHandlerVariable === "string" ? nodeData.customHandlerVariable : "",
    typeof nodeData.currentIndexVariableName === "string" ? nodeData.currentIndexVariableName : "",
    typeof nodeData.iteratorVariableName === "string" ? nodeData.iteratorVariableName : "",
    typeof nodeData.statusCodeVariableName === "string" ? nodeData.statusCodeVariableName : "",
    typeof nodeData.headersVariableName === "string" ? nodeData.headersVariableName : "",
  ].filter(Boolean);
  const action = ((node.data ?? {}) as { action?: MicroflowAction }).action;
  if (!action) {
    return fallbackOutputs;
  }
  const actionRecord = action as unknown as Record<string, unknown>;
  const genericOutputs = [
    typeof actionRecord.outputVariableName === "string" ? actionRecord.outputVariableName : "",
    typeof actionRecord.outputListVariableName === "string" ? actionRecord.outputListVariableName : "",
    typeof actionRecord.resultVariableName === "string" ? actionRecord.resultVariableName : "",
    typeof actionRecord.returnVariableName === "string" ? actionRecord.returnVariableName : "",
    typeof actionRecord.errorVariableName === "string" ? actionRecord.errorVariableName : "",
  ].filter(Boolean);
  if (action.kind === "createVariable") {
    return [...fallbackOutputs, ...genericOutputs, (action as MicroflowCreateVariableAction).variableName].filter(Boolean);
  }
  if (action.kind === "changeVariable") {
    return [...fallbackOutputs, ...genericOutputs, (action as { targetVariableName?: string }).targetVariableName ?? ""].filter(Boolean);
  }
  if (action.kind === "restCall" || action.kind === "restOperationCall") {
    const rest = action as MicroflowRestCallAction;
    const output =
      rest.response.handling.kind === "ignore"
        ? ""
        : rest.response.handling.outputVariableName ?? "";
    return [...fallbackOutputs, ...genericOutputs, output, rest.response.statusCodeVariableName ?? "", rest.response.headersVariableName ?? ""].filter(Boolean);
  }
  if (action.kind === "callMicroflow") {
    const call = action as MicroflowCallMicroflowAction;
    return [...fallbackOutputs, ...genericOutputs, call.returnValue.outputVariableName ?? call.returnValue.resultVariableName ?? ""].filter(Boolean);
  }
  return [...fallbackOutputs, ...genericOutputs].filter(Boolean);
}

function collectReachableUpstreamNodeDepth(schema: MicroflowDesignSchema, nodeId: string): Map<string, number> {
  const incomingByTarget = new Map<string, string[]>();
  for (const edge of schema.workflow.edges) {
    const list = incomingByTarget.get(edge.targetNodeID) ?? [];
    list.push(edge.sourceNodeID);
    incomingByTarget.set(edge.targetNodeID, list);
  }
  const depthByNodeId = new Map<string, number>();
  const queue: Array<{ id: string; depth: number }> = [{ id: nodeId, depth: 0 }];
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current) {
      continue;
    }
    for (const sourceId of incomingByTarget.get(current.id) ?? []) {
      if (sourceId === nodeId) {
        continue;
      }
      const nextDepth = current.depth + 1;
      const existing = depthByNodeId.get(sourceId);
      if (typeof existing === "number" && existing <= nextDepth) {
        continue;
      }
      depthByNodeId.set(sourceId, nextDepth);
      queue.push({ id: sourceId, depth: nextDepth });
    }
  }
  return depthByNodeId;
}

export function buildNodeInlineVariableOptions(input: {
  schema: MicroflowDesignSchema;
  node: MicroflowWorkflowNodeJSON;
  runtimeFrame?: MicroflowTraceFrame;
  mode?: InlineVariableOptionMode;
}): Array<{ label: string; value: string }> {
  const mode = input.mode ?? "expression";
  const list: Array<{ label: string; value: string }> = [];
  const seen = new Set<string>();
  const mergedLabels = new Map<string, string>();
  const refCounts = parseReferenceCounts(input.schema);

  for (const parameter of input.schema.parameters ?? []) {
    pushOption(list, seen, mergedLabels, {
      source: "input",
      name: parameter.name,
      type: parameter.dataType.kind,
      scope: "microflow",
      refCount: refCounts[parameter.name],
    }, mode);
  }

  for (const variable of normalizeDesignVariables(input.schema.variables)) {
    pushOption(list, seen, mergedLabels, {
      source: variable.scope === "latestError" ? "error" : "context",
      name: variable.name,
      type: variable.type.kind,
      scope: variable.scope,
      refCount: refCounts[variable.name],
    }, mode);
  }

  const reachableUpstreamDepth = collectReachableUpstreamNodeDepth(input.schema, input.node.id);
  const nodeTitleById = new Map<string, string>(
    input.schema.workflow.nodes.map(node => [node.id, String((node.data as { title?: string } | undefined)?.title ?? node.id)]),
  );
  for (const [upstreamId, depth] of reachableUpstreamDepth.entries()) {
    const sourceNode = input.schema.workflow.nodes.find(node => node.id === upstreamId);
    if (!sourceNode || sourceNode.id === input.node.id) {
      continue;
    }
    const upstreamSource = depth <= 1 ? "upstream-direct" : "upstream-indirect";
    for (const outputName of extractActionOutputs(sourceNode)) {
      pushOption(list, seen, mergedLabels, {
        source: upstreamSource,
        name: outputName,
        sourceNode: nodeTitleById.get(sourceNode.id) ?? sourceNode.id,
        refCount: refCounts[outputName],
      }, mode);
    }
  }

  const nodeData = (input.node.data ?? {}) as Record<string, unknown>;
  const loopSource = nodeData.loopSource as { listVariableName?: string; iteratorVariableName?: string } | undefined;
  if (loopSource?.listVariableName) {
    pushOption(list, seen, mergedLabels, { source: "loop", name: loopSource.listVariableName }, mode);
  }
  if (loopSource?.iteratorVariableName) {
    pushOption(list, seen, mergedLabels, { source: "loop", name: loopSource.iteratorVariableName }, mode);
  }
  if (typeof nodeData.currentIndexVariableName === "string" && nodeData.currentIndexVariableName) {
    pushOption(list, seen, mergedLabels, { source: "loop", name: nodeData.currentIndexVariableName }, mode);
  }

  pushOption(list, seen, mergedLabels, { source: "system", name: "$currentUser", readonly: true }, mode);
  pushOption(list, seen, mergedLabels, { source: "system", name: "$now", readonly: true }, mode);
  pushOption(list, seen, mergedLabels, { source: "system", name: "$workspaceId", readonly: true }, mode);
  pushOption(list, seen, mergedLabels, { source: "error", name: "$latestError", maybe: true }, mode);

  for (const runtimeVar of Object.values(input.runtimeFrame?.variablesSnapshot ?? {})) {
    pushOption(list, seen, mergedLabels, {
      source: "runtime",
      name: runtimeVar.name,
      type: runtimeVar.type.kind,
      readonly: runtimeVar.readonly,
      scope: runtimeVar.scopeKind,
      preview: runtimeVar.valuePreview,
      refCount: refCounts[runtimeVar.name],
    }, mode);
  }
  const sourceWeight = (label: string): number => {
    if (label.startsWith("upstream-direct::")) {
      return 0;
    }
    if (label.startsWith("upstream-indirect::")) {
      return 1;
    }
    if (label.startsWith("input::")) {
      return 2;
    }
    if (label.startsWith("context::")) {
      return 3;
    }
    if (label.startsWith("loop::")) {
      return 4;
    }
    if (label.startsWith("runtime::")) {
      return 5;
    }
    if (label.startsWith("system::")) {
      return 6;
    }
    if (label.startsWith("error::")) {
      return 7;
    }
    return 9;
  };
  const readRefCount = (label: string): number => {
    const match = label.match(/(?:^|\|)refCount=(\d+)(?:\||$)/);
    return match ? Number(match[1]) : 0;
  };
  return [...list].sort((a, b) => {
    const bySource = sourceWeight(a.label) - sourceWeight(b.label);
    if (bySource !== 0) {
      return bySource;
    }
    const byRef = readRefCount(b.label) - readRefCount(a.label);
    if (byRef !== 0) {
      return byRef;
    }
    return a.value.localeCompare(b.value);
  });
}
