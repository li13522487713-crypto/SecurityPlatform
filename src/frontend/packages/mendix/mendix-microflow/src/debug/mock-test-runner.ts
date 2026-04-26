import { validateMicroflowSchema } from "../schema/validator";
import type {
  MicroflowDataType,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSequenceFlow,
} from "../schema";
import {
  collectRuntimeFlows,
  collectRuntimeObjects,
  getFlowTargetObject,
  getNextNormalFlow,
  getOutgoingErrorHandlerFlows,
  getOutgoingSequenceFlows,
  getStartEvent,
  isExecutableObject,
  selectDecisionFlow,
  selectObjectTypeFlow,
} from "./trace-utils";
import type {
  MicroflowRunSession,
  MicroflowRuntimeError,
  MicroflowRuntimeLog,
  MicroflowRuntimeVariableValue,
  MicroflowTraceFrame,
  MockTestRunMicroflowInput,
} from "./trace-types";

const MAX_STEPS = 500;

function isErrorHandlerFlowId(schema: MockTestRunMicroflowInput["schema"], flowId?: string): boolean {
  if (!flowId) {
    return false;
  }
  return collectRuntimeFlows(schema).some(
    (f): f is MicroflowSequenceFlow => f.kind === "sequence" && f.id === flowId && f.isErrorHandler
  );
}

interface ExecutionState {
  runId: string;
  startedAtMs: number;
  steps: number;
  trace: MicroflowTraceFrame[];
  logs: MicroflowRuntimeLog[];
  values: Record<string, MicroflowRuntimeVariableValue>;
  output?: unknown;
  error?: MicroflowRuntimeError;
}

interface LoopContext {
  loopObjectId: string;
  index: number;
  iteratorVariableName?: string;
  iteratorValuePreview?: string;
}

type ControlSignal = "break" | "continue" | undefined;

export function mockTestRunMicroflow(input: MockTestRunMicroflowInput): Promise<MicroflowRunSession> {
  const runId = `run-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const startedAtMs = Date.now();
  const validation = validateMicroflowSchema({
    schema: input.schema,
    metadata: input.metadata,
    variableIndex: input.variableIndex,
    options: { mode: "testRun", includeWarnings: true },
  });
  const initialValues = buildInitialValues(input);
  const state: ExecutionState = {
    runId,
    startedAtMs,
    steps: 0,
    trace: [],
    logs: [],
    values: initialValues,
  };

  if (validation.summary.errorCount > 0) {
    const error: MicroflowRuntimeError = {
      code: "RUNTIME_VALIDATION_BLOCKED",
      message: `Test run blocked by ${validation.summary.errorCount} validation error(s).`,
      details: validation.issues.filter(issue => issue.severity === "error").map(issue => `${issue.code}: ${issue.message}`).join("\n"),
    };
    state.error = error;
    return Promise.resolve(finishSession(input, state, "failed", error));
  }

  const start = getStartEvent(input.schema);
  if (!start) {
    const error: MicroflowRuntimeError = { code: "RUNTIME_START_NOT_FOUND", message: "No StartEvent found in the microflow." };
    return Promise.resolve(finishSession(input, { ...state, error }, "failed", error));
  }

  executeFromObject(input, state, start, undefined, undefined);
  const status = state.error ? "failed" : "success";
  return Promise.resolve(finishSession(input, state, status, state.error));
}

function executeFromObject(
  input: MockTestRunMicroflowInput,
  state: ExecutionState,
  object: MicroflowObject | undefined,
  incomingFlowId?: string,
  loop?: LoopContext,
): ControlSignal {
  let current = object;
  let incoming = incomingFlowId;
  const visited = new Set<string>();
  while (current && !state.error) {
    state.steps += 1;
    if (state.steps > MAX_STEPS) {
      state.error = {
        code: "RUNTIME_MAX_STEPS_EXCEEDED",
        message: `Mock runtime stopped after ${MAX_STEPS} steps to prevent an infinite loop.`,
        objectId: current.id,
        flowId: incoming,
      };
      pushFrame(input, state, current, { incomingFlowId: incoming, status: "failed", error: state.error, loop });
      return undefined;
    }
    if (visited.has(`${current.id}:${loop?.index ?? "root"}`)) {
      state.logs.push(log(state, "warning", current.id, undefined, "Repeated object detected; mock runtime follows each object once per loop iteration."));
      return undefined;
    }
    visited.add(`${current.id}:${loop?.index ?? "root"}`);
    if (!isExecutableObject(current)) {
      return undefined;
    }

    if (current.kind === "breakEvent") {
      pushFrame(input, state, current, { incomingFlowId: incoming, status: "success", output: { signal: "break" }, loop, message: "Break current loop." });
      return "break";
    }
    if (current.kind === "continueEvent") {
      pushFrame(input, state, current, { incomingFlowId: incoming, status: "success", output: { signal: "continue" }, loop, message: "Continue next loop iteration." });
      return "continue";
    }
    if (current.kind === "errorEvent") {
      state.error = {
        code: "RUNTIME_MICROFLOW_ERROR_EVENT",
        message: "Mock runtime reached an ErrorEvent.",
        objectId: current.id,
        flowId: incoming,
      };
      pushFrame(input, state, current, { incomingFlowId: incoming, status: "failed", error: state.error, loop });
      return undefined;
    }
    if (current.kind === "endEvent") {
      state.output = current.returnValue?.raw ?? { status: "completed" };
      pushFrame(input, state, current, { incomingFlowId: incoming, status: "success", output: { returnValue: state.output }, loop });
      return undefined;
    }
    if (current.kind === "loopedActivity") {
      const afterLoop = getNextNormalFlow(input.schema, current.id);
      runLoop(input, state, current, incoming, loop);
      pushFrame(input, state, current, { incomingFlowId: incoming, outgoingFlowId: afterLoop?.id, status: state.error ? "failed" : "success", loop });
      incoming = afterLoop?.id;
      current = afterLoop ? getFlowTargetObject(input.schema, afterLoop) : undefined;
      continue;
    }

    const selected = selectOutgoingFlow(input, state, current);
    const frameError = selected.error;
    const status = frameError ? "failed" : "success";
    applyActionEffects(input, state, current, frameError);
    pushFrame(input, state, current, {
      incomingFlowId: incoming,
      outgoingFlowId: selected.flow?.id,
      selectedCaseValue: selected.selectedCaseValue,
      status,
      error: frameError,
      output: selected.output,
      loop,
      message: selected.message,
      errorHandlerVisited: selected.errorHandlerVisited
    });
    if (frameError && !selected.flow) {
      state.error = frameError;
      return undefined;
    }
    incoming = selected.flow?.id;
    current = selected.flow ? getFlowTargetObject(input.schema, selected.flow) : undefined;
  }
  return undefined;
}

function runLoop(
  input: MockTestRunMicroflowInput,
  state: ExecutionState,
  loopObject: Extract<MicroflowObject, { kind: "loopedActivity" }>,
  incomingFlowId?: string,
  parentLoop?: LoopContext,
): void {
  const iterations = Math.max(0, Math.min(input.options?.loopIterations ?? 2, 50));
  for (let index = 0; index < iterations; index += 1) {
    const iteratorVariableName = loopObject.loopSource.kind === "iterableList" ? loopObject.loopSource.iteratorVariableName : undefined;
    const loopContext: LoopContext = {
      loopObjectId: loopObject.id,
      index,
      iteratorVariableName,
      iteratorValuePreview: iteratorVariableName ? `${iteratorVariableName}[${index}]` : `iteration ${index}`,
    };
    if (iteratorVariableName) {
      state.values[iteratorVariableName] = {
        name: iteratorVariableName,
        type: { kind: "object", entityQualifiedName: "Mock.Iterator" },
        valuePreview: `${iteratorVariableName}[${index}]`,
        source: "loopIterator",
      };
    }
    state.values.$currentIndex = {
      name: "$currentIndex",
      type: { kind: "integer" },
      valuePreview: String(index),
      rawValue: index,
      source: "system",
    };
    pushFrame(input, state, loopObject, {
      incomingFlowId,
      status: "success",
      loop: parentLoop,
      output: { currentIndex: index, iteratorVariableName },
      message: `Loop iteration ${index}`,
    });
    const start = getStartEvent(input.schema, loopObject.objectCollection) ?? firstExecutable(loopObject.objectCollection);
    const signal = executeFromObject(input, state, start, undefined, loopContext);
    if (state.error || signal === "break") {
      break;
    }
  }
}

function selectOutgoingFlow(
  input: MockTestRunMicroflowInput,
  state: ExecutionState,
  object: MicroflowObject,
): {
  flow?: MicroflowSequenceFlow;
  selectedCaseValue?: MicroflowTraceFrame["selectedCaseValue"];
  error?: MicroflowRuntimeError;
  output?: Record<string, unknown>;
  message?: string;
  errorHandlerVisited?: boolean;
} {
  if (object.kind === "exclusiveSplit") {
    const result = selectDecisionFlow(input.schema, object, input.options);
    if (result.warning) {
      state.logs.push(log(state, "warning", object.id, undefined, result.warning));
    }
    return { flow: result.flow, selectedCaseValue: result.selectedCaseValue, output: { selectedCaseValue: result.selectedCaseValue } };
  }
  if (object.kind === "inheritanceSplit") {
    const result = selectObjectTypeFlow(input.schema, object, input.options);
    if (result.warning) {
      state.logs.push(log(state, "warning", object.id, undefined, result.warning));
    }
    return { flow: result.flow, selectedCaseValue: result.selectedCaseValue, output: { selectedCaseValue: result.selectedCaseValue } };
  }
  const isRestError = object.kind === "actionActivity" && object.action.kind === "restCall" && input.options?.simulateRestError === true;
  if (isRestError) {
    const errorFlow = getOutgoingErrorHandlerFlows(input.schema, object.id)[0];
    const error: MicroflowRuntimeError = {
      code: "RUNTIME_REST_CALL_FAILED",
      message: "Mock REST call failed by test option simulateRestError.",
      objectId: object.id,
      actionId: object.action.id,
      flowId: errorFlow?.id,
      details: "No real REST request was sent.",
    };
    state.values.$latestError = {
      name: "$latestError",
      type: { kind: "object", entityQualifiedName: "System.Error" },
      valuePreview: error.message,
      rawValue: error,
      source: "errorContext",
    };
    state.values.$latestHttpResponse = {
      name: "$latestHttpResponse",
      type: { kind: "json" },
      valuePreview: "{ status: 500 }",
      rawValue: { status: 500, body: "mock error" },
      source: "restResponse",
    };
    return {
      flow: errorFlow,
      error,
      output: { errorHandled: Boolean(errorFlow) },
      errorHandlerVisited: Boolean(errorFlow)
    };
  }
  return { flow: getNextNormalFlow(input.schema, object.id), output: actionOutput(object) };
}

function applyActionEffects(input: MockTestRunMicroflowInput, state: ExecutionState, object: MicroflowObject, error?: MicroflowRuntimeError): void {
  if (object.kind !== "actionActivity" || error) {
    return;
  }
  const action = object.action;
  if (action.kind === "retrieve") {
    const isList = action.retrieveSource.kind === "database" && action.retrieveSource.range.kind === "all";
    const entityQ =
      action.retrieveSource.kind === "database" ? action.retrieveSource.entityQualifiedName ?? "Mock.Entity" : "Mock.Association";
    state.values[action.outputVariableName] = {
      name: action.outputVariableName,
      type: isList ? { kind: "list", itemType: { kind: "object", entityQualifiedName: entityQ } } : { kind: "object", entityQualifiedName: entityQ },
      valuePreview: isList ? "[mock object, mock object]" : "{ mock: true }",
      rawValue: isList ? [{ id: 1 }, { id: 2 }] : { id: 1 },
      source: "retrieve",
    };
  }
  if (action.kind === "createObject") {
    state.values[action.outputVariableName] = {
      name: action.outputVariableName,
      type: { kind: "object", entityQualifiedName: action.entityQualifiedName },
      valuePreview: `{ ${action.entityQualifiedName} }`,
      rawValue: { entity: action.entityQualifiedName },
      source: "createObject",
    };
  }
  if (action.kind === "createVariable") {
    const iv = action.initialValue;
    state.values[action.variableName] = {
      name: action.variableName,
      type: action.dataType,
      valuePreview: (iv && "raw" in iv ? iv.raw : undefined) || previewForType(action.dataType),
      rawValue: iv && "raw" in iv ? iv.raw : undefined,
      source: "createVariable",
    };
  }
  if (action.kind === "changeVariable") {
    state.values[action.targetVariableName] = {
      name: action.targetVariableName,
      type: state.values[action.targetVariableName]?.type ?? { kind: "unknown", reason: "mock changeVariable" },
      valuePreview: action.newValueExpression.raw,
      rawValue: action.newValueExpression.raw,
      source: "changeVariable",
    };
  }
  if (action.kind === "restCall") {
    const handling = action.response.handling;
    if (handling.kind !== "ignore") {
      state.values[handling.outputVariableName] = {
        name: handling.outputVariableName,
        type: handling.kind === "string" ? { kind: "string" } : { kind: "json" },
        valuePreview: handling.kind === "string" ? "mock response" : "{ ok: true }",
        rawValue: handling.kind === "string" ? "mock response" : { ok: true },
        source: "restResponse",
      };
    }
  }
  if (action.kind === "logMessage") {
    state.logs.push(log(state, action.level, object.id, action.id, action.template.text || "Mock log message"));
  }
}

function pushFrame(
  input: MockTestRunMicroflowInput,
  state: ExecutionState,
  object: MicroflowObject,
  options: {
    incomingFlowId?: string;
    outgoingFlowId?: string;
    selectedCaseValue?: MicroflowTraceFrame["selectedCaseValue"];
    status: MicroflowTraceFrame["status"];
    output?: Record<string, unknown>;
    error?: MicroflowRuntimeError;
    loop?: LoopContext;
    message?: string;
    errorHandlerVisited?: boolean;
  },
): void {
  const startedAt = state.startedAtMs + state.trace.length * 14;
  const endedAt = startedAt + 8;
  const actionId = object.kind === "actionActivity" ? object.action.id : undefined;
  const frameId = `${state.runId}-${state.trace.length + 1}`;
  const snapshot = { ...state.values };
  const errorHandlerVisited =
    options.errorHandlerVisited ??
    (isErrorHandlerFlowId(input.schema, options.incomingFlowId) || isErrorHandlerFlowId(input.schema, options.outgoingFlowId));
  const frame: MicroflowTraceFrame = {
    id: frameId,
    frameId,
    runId: state.runId,
    objectId: object.id,
    nodeId: object.id,
    objectTitle: titleForObject(object),
    nodeTitle: titleForObject(object),
    actionId,
    collectionId: collectionIdForObject(input.schema.objectCollection, object.id),
    incomingFlowId: options.incomingFlowId,
    outgoingFlowId: options.outgoingFlowId,
    incomingEdgeId: options.incomingFlowId,
    outgoingEdgeId: options.outgoingFlowId,
    selectedCaseValue: options.selectedCaseValue,
    loopIteration: options.loop,
    errorHandlerVisited,
    status: options.status,
    startedAt: new Date(startedAt).toISOString(),
    endedAt: new Date(endedAt).toISOString(),
    durationMs: endedAt - startedAt,
    input: state.trace.length === 0 ? input.parameters : { incomingFlowId: options.incomingFlowId },
    output: options.output,
    error: options.error,
    variablesSnapshot: snapshot,
    message: options.message,
  };
  state.trace.push(frame);
}

function finishSession(
  input: MockTestRunMicroflowInput,
  state: ExecutionState,
  status: MicroflowRunSession["status"],
  error?: MicroflowRuntimeError,
): MicroflowRunSession {
  return {
    id: state.runId,
    schemaId: input.schema.id,
    startedAt: new Date(state.startedAtMs).toISOString(),
    endedAt: new Date(state.startedAtMs + Math.max(1, state.trace.length) * 14).toISOString(),
    status,
    input: input.parameters,
    output: state.output,
    error,
    trace: state.trace,
    logs: state.logs,
    variables: state.trace.map(frame => ({
      frameId: frame.id,
      objectId: frame.objectId,
      variables: Object.values(frame.variablesSnapshot ?? {}),
    })),
  };
}

function buildInitialValues(input: MockTestRunMicroflowInput): Record<string, MicroflowRuntimeVariableValue> {
  const values: Record<string, MicroflowRuntimeVariableValue> = {};
  for (const parameter of input.schema.parameters) {
    values[parameter.name] = {
      name: parameter.name,
      type: parameter.dataType,
      valuePreview: previewValue(input.parameters[parameter.name], parameter.dataType),
      rawValue: input.parameters[parameter.name],
      source: "parameter",
    };
  }
  return values;
}

function previewValue(value: unknown, dataType: MicroflowDataType): string {
  if (value === undefined || value === null || value === "") {
    return previewForType(dataType);
  }
  if (typeof value === "object") {
    return JSON.stringify(value);
  }
  return String(value);
}

function previewForType(dataType: MicroflowDataType): string {
  if (dataType.kind === "object") {
    return `{ ${dataType.entityQualifiedName} }`;
  }
  if (dataType.kind === "list") {
    return "[]";
  }
  return dataType.kind;
}

function titleForObject(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    return object.caption || object.action.caption || object.action.kind;
  }
  return object.caption ?? object.kind;
}

function firstExecutable(collection: MicroflowObjectCollection): MicroflowObject | undefined {
  return collection.objects.find(object => isExecutableObject(object));
}

function collectionIdForObject(collection: MicroflowObjectCollection, objectId: string): string | undefined {
  if (collection.objects.some(object => object.id === objectId)) {
    return collection.id;
  }
  for (const object of collection.objects) {
    if (object.kind === "loopedActivity") {
      const found = collectionIdForObject(object.objectCollection, objectId);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

function actionOutput(object: MicroflowObject): Record<string, unknown> {
  if (object.kind !== "actionActivity") {
    return { objectKind: object.kind };
  }
  if (object.action.kind === "changeMembers") {
    return { changedVariableName: object.action.changeVariableName, changedMembers: object.action.memberChanges.length };
  }
  if (object.action.kind === "commit") {
    return { committed: object.action.objectOrListVariableName };
  }
  return { actionKind: object.action.kind };
}

function log(state: ExecutionState, level: MicroflowRuntimeLog["level"], objectId: string, actionId: string | undefined, message: string): MicroflowRuntimeLog {
  return {
    id: `${state.runId}-log-${state.logs.length + 1}`,
    timestamp: new Date(state.startedAtMs + state.logs.length * 9).toISOString(),
    level,
    objectId,
    actionId,
    message,
  };
}
