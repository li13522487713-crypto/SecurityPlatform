import type { MicroflowTraceFrame } from "../debug/trace-types";
import type {
  MicroflowAction,
  MicroflowCallMicroflowAction,
  MicroflowCreateVariableAction,
  MicroflowDesignSchema,
  MicroflowRestCallAction,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";

type InlineVariableOptionMode = "name" | "expression";

interface PushOptionInput {
  source: "input" | "context" | "upstream" | "loop" | "system" | "error" | "runtime";
  name: string;
  type?: string;
  sourceNode?: string;
  scope?: string;
  readonly?: boolean;
  maybe?: boolean;
  unknown?: boolean;
  preview?: string;
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

function pushOption(
  list: Array<{ label: string; value: string }>,
  seen: Set<string>,
  input: PushOptionInput,
  mode: InlineVariableOptionMode,
) {
  const value = normalizeName(input.name, mode);
  if (!value || seen.has(value)) {
    return;
  }
  seen.add(value);
  const metadata = [
    input.type ? `type=${input.type}` : "",
    input.sourceNode ? `sourceNode=${input.sourceNode}` : "",
    input.scope ? `scope=${input.scope}` : "",
    input.readonly ? "readonly=true" : "",
    input.maybe ? "maybe=true" : "",
    input.unknown ? "unknown=true" : "",
    input.preview ? `preview=${input.preview}` : "",
  ].filter(Boolean);
  const suffix = metadata.length > 0 ? `|${metadata.join("|")}` : "";
  list.push({
    label: `${input.source}::${value}${suffix}`,
    value,
  });
}

function extractActionOutputs(node: MicroflowWorkflowNodeJSON): string[] {
  const action = ((node.data ?? {}) as { action?: MicroflowAction }).action;
  if (!action) {
    return [];
  }
  if (action.kind === "createVariable") {
    return [(action as MicroflowCreateVariableAction).variableName].filter(Boolean);
  }
  if (action.kind === "changeVariable") {
    return [(action as { targetVariableName?: string }).targetVariableName ?? ""].filter(Boolean);
  }
  if (action.kind === "restCall" || action.kind === "restOperationCall") {
    const rest = action as MicroflowRestCallAction;
    const output =
      rest.response.handling.kind === "ignore"
        ? ""
        : rest.response.handling.outputVariableName ?? "";
    return [output, rest.response.statusCodeVariableName ?? "", rest.response.headersVariableName ?? ""].filter(Boolean);
  }
  if (action.kind === "callMicroflow") {
    const call = action as MicroflowCallMicroflowAction;
    return [call.returnValue.outputVariableName ?? call.returnValue.resultVariableName ?? ""].filter(Boolean);
  }
  return [];
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

  for (const parameter of input.schema.parameters) {
    pushOption(list, seen, {
      source: "input",
      name: parameter.name,
      type: parameter.dataType.kind,
      scope: "microflow",
    }, mode);
  }

  for (const variable of input.schema.variables) {
    pushOption(list, seen, {
      source: variable.scope === "latestError" ? "error" : "context",
      name: variable.name,
      type: variable.type.kind,
      scope: variable.scope,
    }, mode);
  }

  const upstreamEdges = input.schema.workflow.edges.filter(edge => edge.targetNodeID === input.node.id);
  const nodeTitleById = new Map<string, string>(
    input.schema.workflow.nodes.map(node => [node.id, String((node.data as { title?: string } | undefined)?.title ?? node.id)]),
  );
  for (const edge of upstreamEdges) {
    const sourceNode = input.schema.workflow.nodes.find(node => node.id === edge.sourceNodeID);
    if (!sourceNode) {
      continue;
    }
    for (const outputName of extractActionOutputs(sourceNode)) {
      pushOption(list, seen, {
        source: "upstream",
        name: outputName,
        sourceNode: nodeTitleById.get(sourceNode.id) ?? sourceNode.id,
      }, mode);
    }
  }

  const nodeData = (input.node.data ?? {}) as Record<string, unknown>;
  const loopSource = nodeData.loopSource as { listVariableName?: string; iteratorVariableName?: string } | undefined;
  if (loopSource?.listVariableName) {
    pushOption(list, seen, { source: "loop", name: loopSource.listVariableName }, mode);
  }
  if (loopSource?.iteratorVariableName) {
    pushOption(list, seen, { source: "loop", name: loopSource.iteratorVariableName }, mode);
  }
  if (typeof nodeData.currentIndexVariableName === "string" && nodeData.currentIndexVariableName) {
    pushOption(list, seen, { source: "loop", name: nodeData.currentIndexVariableName }, mode);
  }

  pushOption(list, seen, { source: "system", name: "$currentUser", readonly: true }, mode);
  pushOption(list, seen, { source: "system", name: "$now", readonly: true }, mode);
  pushOption(list, seen, { source: "system", name: "$workspaceId", readonly: true }, mode);
  pushOption(list, seen, { source: "error", name: "$latestError", maybe: true }, mode);

  for (const runtimeVar of Object.values(input.runtimeFrame?.variablesSnapshot ?? {})) {
    pushOption(list, seen, {
      source: "runtime",
      name: runtimeVar.name,
      type: runtimeVar.type.kind,
      readonly: runtimeVar.readonly,
      scope: runtimeVar.scopeKind,
      preview: runtimeVar.valuePreview,
    }, mode);
  }

  return list;
}
