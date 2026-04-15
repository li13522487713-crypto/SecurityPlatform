import {
  normalizeNodeTypeKey,
  workflowNodeTypeToValue,
  workflowNodeValueToKey,
  type CanvasSchema,
  type ConnectionSchema,
  type NodeSchema,
  type WorkflowCanvasPayload
} from "./types";

const DEFAULT_NODE_WIDTH = 160;
const DEFAULT_NODE_HEIGHT = 60;

export function toBackendCanvasJson(editorCanvasJson: string): string {
  let parsed: unknown;
  try {
    parsed = JSON.parse(editorCanvasJson);
  } catch {
    return editorCanvasJson;
  }
  if (!parsed || typeof parsed !== "object") {
    return editorCanvasJson;
  }

  if (isEditorCanvasPayload(parsed)) {
    parsed = normalizeEditorCanvasPayload(parsed);
  }

  if (isLegacyBackendCanvasPayload(parsed)) {
    return editorCanvasJson;
  }

  const editorCanvas = parsed as CanvasSchema;
  const nodes = (editorCanvas.nodes ?? []).map((node) => {
    const editorNode = node as NodeSchema;
    const normalizedType = normalizeNodeTypeKey(String(editorNode.type));
    const config = normalizeConfigPayload(editorNode.configs);
    if (editorNode.inputMappings && Object.keys(editorNode.inputMappings).length > 0) {
      config.inputMappings = editorNode.inputMappings;
    }
    return {
      key: editorNode.key,
      type: workflowNodeTypeToValue(normalizedType),
      label: editorNode.title ?? normalizedType,
      config,
      layout: {
        x: editorNode.layout?.x ?? 0,
        y: editorNode.layout?.y ?? 0,
        width: editorNode.layout?.width ?? DEFAULT_NODE_WIDTH,
        height: editorNode.layout?.height ?? DEFAULT_NODE_HEIGHT
      },
      childCanvas: editorNode.childCanvas ? JSON.parse(toBackendCanvasJson(JSON.stringify(editorNode.childCanvas))) : undefined,
      inputTypes: editorNode.inputTypes,
      outputTypes: editorNode.outputTypes,
      inputSources: editorNode.inputSources,
      outputSources: editorNode.outputSources,
      ports: editorNode.ports,
      version: editorNode.version,
      debugMeta: editorNode.debugMeta
    };
  });

  const connections = (editorCanvas.connections ?? []).map((connection) => {
    const editorConnection = connection as ConnectionSchema;
    return {
      sourceNodeKey: editorConnection.fromNode,
      sourcePort: editorConnection.fromPort ?? "output",
      targetNodeKey: editorConnection.toNode,
      targetPort: editorConnection.toPort ?? "input",
      condition: editorConnection.condition ?? null
    };
  });
  return JSON.stringify({
    nodes,
    connections,
    schemaVersion: editorCanvas.schemaVersion,
    viewport: editorCanvas.viewport,
    globals: editorCanvas.globals
  });
}

export function toEditorCanvasJson(backendCanvasJson: string): string {
  let parsed: unknown;
  try {
    parsed = JSON.parse(backendCanvasJson);
  } catch {
    return backendCanvasJson;
  }
  if (!parsed || typeof parsed !== "object") {
    return backendCanvasJson;
  }

  if (isEditorCanvasPayload(parsed)) {
    return JSON.stringify(normalizeEditorCanvasPayload(parsed));
  }

  if (!isLegacyBackendCanvasPayload(parsed)) {
    return backendCanvasJson;
  }

  const payload = parsed as WorkflowCanvasPayload;
  const nodes = payload.nodes.map((node) => {
    const config = normalizeConfigPayload(node.config ?? {});
    const inputMappings = normalizeInputMappings(config.inputMappings);
    delete config.inputMappings;
    return {
      key: node.key,
      type: workflowNodeValueToKey(node.type),
      title: node.label ?? node.key,
      layout: {
        x: node.layout?.x ?? 0,
        y: node.layout?.y ?? 0,
        width: node.layout?.width ?? DEFAULT_NODE_WIDTH,
        height: node.layout?.height ?? DEFAULT_NODE_HEIGHT
      },
      configs: config,
      inputMappings,
      childCanvas: node.childCanvas ? JSON.parse(toEditorCanvasJson(JSON.stringify(node.childCanvas))) : undefined,
      inputTypes: node.inputTypes,
      outputTypes: node.outputTypes,
      inputSources: node.inputSources,
      outputSources: node.outputSources,
      ports: node.ports,
      version: node.version,
      debugMeta: node.debugMeta
    };
  });
  const connections = payload.connections.map((connection) => ({
    fromNode: connection.sourceNodeKey,
    fromPort: connection.sourcePort ?? "output",
    toNode: connection.targetNodeKey,
    toPort: connection.targetPort ?? "input",
    condition: connection.condition ?? null
  }));
  return JSON.stringify({
    nodes: normalizeEditorCanvasPayload({ nodes, connections, schemaVersion: payload.schemaVersion, viewport: payload.viewport, globals: payload.globals }).nodes,
    connections,
    schemaVersion: payload.schemaVersion,
    viewport: payload.viewport,
    globals: payload.globals
  });
}

export function normalizeEditorCanvasPayload(canvas: CanvasSchema): CanvasSchema {
  return {
    ...canvas,
    nodes: (canvas.nodes ?? []).map((node) => ({
      ...node,
      configs: normalizeEditorNodeConfig(String(node.type), normalizeConfigPayload(node.configs)),
      childCanvas: node.childCanvas ? normalizeEditorCanvasPayload(node.childCanvas) : undefined
    }))
  };
}

function normalizeConfigPayload(config: unknown): Record<string, unknown> {
  if (!config || typeof config !== "object" || Array.isArray(config)) {
    return {};
  }
  return { ...(config as Record<string, unknown>) };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isEditorCanvasPayload(value: unknown): value is CanvasSchema {
  if (!isRecord(value) || !Array.isArray(value.nodes) || !Array.isArray(value.connections)) {
    return false;
  }

  const sampleNode = value.nodes.find(isRecord);
  const sampleConnection = value.connections.find(isRecord);

  return Boolean(
    sampleNode &&
      ("title" in sampleNode || "configs" in sampleNode || "inputMappings" in sampleNode || "childCanvas" in sampleNode)
  ) || Boolean(sampleConnection && ("fromNode" in sampleConnection || "toNode" in sampleConnection));
}

function isLegacyBackendCanvasPayload(value: unknown): value is WorkflowCanvasPayload {
  if (!isRecord(value) || !Array.isArray(value.nodes) || !Array.isArray(value.connections)) {
    return false;
  }

  const sampleNode = value.nodes.find(isRecord);
  const sampleConnection = value.connections.find(isRecord);

  return Boolean(
    sampleNode &&
      ("label" in sampleNode || "config" in sampleNode || typeof sampleNode.type === "number")
  ) || Boolean(sampleConnection && ("sourceNodeKey" in sampleConnection || "targetNodeKey" in sampleConnection));
}

function normalizeEditorNodeConfig(type: string, config: Record<string, unknown>): Record<string, unknown> {
  const next = { ...config };
  const nestedEntry = isRecord(config.entry) ? config.entry : null;
  const nestedLlm = isRecord(config.llm) ? config.llm : null;
  const nestedExit = isRecord(config.exit) ? config.exit : null;

  if (type === "Entry") {
    const entryVariable = firstNonEmptyString(next.entryVariable, next.variable, nestedEntry?.entryVariable, nestedEntry?.variable);
    const entryDescription = firstNonEmptyString(next.entryDescription, next.description, nestedEntry?.entryDescription, nestedEntry?.description);
    const entryAutoSaveHistory = firstBoolean(next.entryAutoSaveHistory, next.autoSaveHistory, nestedEntry?.entryAutoSaveHistory, nestedEntry?.autoSaveHistory);

    if (entryVariable) {
      next.entryVariable = entryVariable;
    }
    if (entryDescription) {
      next.entryDescription = entryDescription;
    }
    if (typeof entryAutoSaveHistory === "boolean") {
      next.entryAutoSaveHistory = entryAutoSaveHistory;
    }
  }

  if (type === "Llm") {
    const provider = firstNonEmptyString(next.provider, nestedLlm?.provider);
    const model = firstNonEmptyString(next.model, nestedLlm?.model);
    const prompt = firstNonEmptyString(next.prompt, next.userPrompt, nestedLlm?.prompt, nestedLlm?.userPrompt);
    const outputKey = firstNonEmptyString(next.outputKey, nestedLlm?.outputKey, "result");
    const systemPrompt = firstNonEmptyString(next.systemPrompt, nestedLlm?.systemPrompt);

    if (provider) {
      next.provider = provider;
    }
    if (model) {
      next.model = model;
    }
    if (prompt) {
      next.prompt = prompt;
    }
    if (outputKey) {
      next.outputKey = outputKey;
    }
    if (systemPrompt) {
      next.systemPrompt = systemPrompt;
    }
  }

  if (type === "Exit") {
    const exitTerminateMode = firstNonEmptyString(next.exitTerminateMode, next.terminateMode, nestedExit?.exitTerminateMode, nestedExit?.terminateMode);
    const exitTemplate = firstNonEmptyString(next.exitTemplate, next.template, nestedExit?.exitTemplate, nestedExit?.template);
    const exitStreaming = firstBoolean(next.exitStreaming, next.streaming, nestedExit?.exitStreaming, nestedExit?.streaming);

    if (exitTerminateMode) {
      next.exitTerminateMode = exitTerminateMode;
    }
    if (exitTemplate) {
      next.exitTemplate = exitTemplate;
    }
    if (typeof exitStreaming === "boolean") {
      next.exitStreaming = exitStreaming;
    }
  }

  return next;
}

function firstNonEmptyString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function firstBoolean(...values: unknown[]): boolean | undefined {
  for (const value of values) {
    if (typeof value === "boolean") {
      return value;
    }
  }
  return undefined;
}

function normalizeInputMappings(raw: unknown): Record<string, string> {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return {};
  }
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === "string") {
      result[key] = value;
    }
  }
  return result;
}
