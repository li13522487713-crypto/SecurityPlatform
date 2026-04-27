import type {
  MicroflowDiscriminatedRuntimeP0ActionDto,
} from "../schema";
import type {
  MicroflowExecutionFlow,
  MicroflowExecutionNode,
  MicroflowExecutionPlan,
} from "../runtime";
import { validateExecutionPlan } from "../runtime";
import type {
  MicroflowRunSession,
  MicroflowRuntimeError,
  MicroflowRuntimeLog,
  MicroflowRuntimeVariableValue,
  MicroflowTestRunOptions,
  MicroflowTraceFrame,
} from "./trace-types";

const MAX_STEPS = 500;

export interface MockRunExecutionPlanInput {
  parameters: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
}

interface PlanRunState {
  runId: string;
  startedAtMs: number;
  steps: number;
  trace: MicroflowTraceFrame[];
  logs: MicroflowRuntimeLog[];
  values: Record<string, MicroflowRuntimeVariableValue>;
  output?: unknown;
  error?: MicroflowRuntimeError;
}

interface PlanLoopContext {
  loopObjectId: string;
  index: number;
  iteratorVariableName?: string;
  iteratorValuePreview?: string;
}

type ControlSignal = "break" | "continue" | undefined;

export function mockRunExecutionPlan(plan: MicroflowExecutionPlan, input: MockRunExecutionPlanInput): MicroflowRunSession {
  const runId = `run-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const startedAtMs = Date.now();
  const state: PlanRunState = {
    runId,
    startedAtMs,
    steps: 0,
    trace: [],
    logs: [],
    values: buildInitialPlanValues(plan, input.parameters),
  };
  const validation = validateExecutionPlan(plan);
  if (!validation.valid) {
    const error: MicroflowRuntimeError = {
      code: validation.issues.some(issue => issue.code === "RUNTIME_START_NOT_FOUND") ? "RUNTIME_START_NOT_FOUND" : "RUNTIME_UNKNOWN_ERROR",
      message: "ExecutionPlan validation failed.",
      details: validation.issues.map(issue => `${issue.code}: ${issue.message}`).join("\n"),
    };
    return finishPlanSession(plan, input, { ...state, error }, "failed", error);
  }
  executePlanFromNode(plan, input, state, nodeById(plan).get(plan.startNodeId));
  const status = state.error ? "failed" : "success";
  return finishPlanSession(plan, input, state, status, state.error);
}

export function buildRunSessionRuntimeHighlightState(session: MicroflowRunSession): {
  activeObjectId?: string;
  activeFlowId?: string;
  nodeStates: Record<string, MicroflowTraceFrame["status"]>;
  flowStates: Record<string, "visited" | "errorHandlerVisited" | "skipped">;
} {
  const nodeStates: Record<string, MicroflowTraceFrame["status"]> = {};
  const flowStates: Record<string, "visited" | "errorHandlerVisited" | "skipped"> = {};
  for (const frame of session.trace) {
    nodeStates[frame.objectId] = frame.status;
    if (frame.outgoingFlowId) {
      flowStates[frame.outgoingFlowId] = frame.errorHandlerVisited ? "errorHandlerVisited" : "visited";
    }
  }
  const active = [...session.trace].reverse()[0];
  return { activeObjectId: active?.objectId, activeFlowId: active?.outgoingFlowId, nodeStates, flowStates };
}

export function createRunSessionFromExecutionResult(result: MicroflowRunSession): MicroflowRunSession {
  return result;
}

function executePlanFromNode(
  plan: MicroflowExecutionPlan,
  input: MockRunExecutionPlanInput,
  state: PlanRunState,
  object: MicroflowExecutionNode | undefined,
  incomingFlowId?: string,
  loop?: PlanLoopContext,
): ControlSignal {
  let current = object;
  let incoming = incomingFlowId;
  const seen = new Set<string>();
  const nodes = nodeById(plan);
  while (current && !state.error) {
    state.steps += 1;
    if (state.steps > MAX_STEPS) {
      const error: MicroflowRuntimeError = {
        code: "RUNTIME_MAX_STEPS_EXCEEDED",
        message: `Mock runtime stopped after ${MAX_STEPS} steps to prevent an infinite loop.`,
        objectId: current.objectId,
        flowId: incoming,
      };
      state.error = error;
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "failed", error, loop });
      return undefined;
    }
    const seenKey = `${current.objectId}:${loop?.index ?? "root"}`;
    if (seen.has(seenKey)) {
      state.logs.push(log(state, "warning", current.objectId, current.actionId, "Repeated object detected; mock runtime follows each object once per loop iteration."));
      return undefined;
    }
    seen.add(seenKey);

    if (current.runtimeBehavior === "ignored") {
      return undefined;
    }
    if (current.runtimeBehavior === "unsupported") {
      const unsupported = plan.unsupportedActions.find(item => item.objectId === current?.objectId);
      const error: MicroflowRuntimeError = {
        code: unsupported?.reason === "requiresConnector" ? "RUNTIME_CONNECTOR_REQUIRED" : "RUNTIME_UNSUPPORTED_ACTION",
        message: unsupported?.message ?? "该节点仅支持建模，当前 Runtime 不支持执行。",
        objectId: current.objectId,
        actionId: current.actionId,
      };
      state.error = error;
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "failed", error, loop });
      return undefined;
    }
    if (current.kind === "breakEvent") {
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "success", output: { signal: "break" }, loop, message: "break" });
      return "break";
    }
    if (current.kind === "continueEvent") {
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "success", output: { signal: "continue" }, loop, message: "continue" });
      return "continue";
    }
    if (current.kind === "errorEvent") {
      const error: MicroflowRuntimeError = { code: "RUNTIME_MICROFLOW_ERROR_EVENT", message: "Mock runtime reached an ErrorEvent.", objectId: current.objectId, flowId: incoming };
      state.error = error;
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "failed", error, loop });
      return undefined;
    }
    if (current.kind === "endEvent") {
      state.output = { status: "completed" };
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, status: "success", output: { returnValue: state.output }, loop });
      return undefined;
    }
    if (current.kind === "loopedActivity") {
      runPlanLoop(plan, input, state, current, incoming, loop);
      const afterLoop = nextNormalFlow(plan, current.objectId);
      pushPlanFrame(plan, state, current, { incomingFlowId: incoming, outgoingFlowId: afterLoop?.flowId, status: state.error ? "failed" : "success", loop });
      incoming = afterLoop?.flowId;
      current = afterLoop ? nodes.get(afterLoop.destinationObjectId) : undefined;
      continue;
    }

    const selected = selectPlanOutgoingFlow(plan, input, state, current);
    if (!selected.flow && selected.error) {
      state.error = selected.error;
    }
    applyPlanActionEffects(plan, input, state, current, selected.error);
    pushPlanFrame(plan, state, current, {
      incomingFlowId: incoming,
      outgoingFlowId: selected.flow?.flowId,
      selectedCaseValue: selected.selectedCaseValue,
      status: selected.error && !selected.flow ? "failed" : "success",
      error: selected.error,
      output: selected.output,
      loop,
      message: selected.message,
      errorHandlerVisited: selected.flow?.controlFlow === "errorHandler",
    });
    if (state.error) {
      return undefined;
    }
    incoming = selected.flow?.flowId;
    current = selected.flow ? nodes.get(selected.flow.destinationObjectId) : undefined;
  }
  return undefined;
}

function runPlanLoop(plan: MicroflowExecutionPlan, input: MockRunExecutionPlanInput, state: PlanRunState, loopNode: MicroflowExecutionNode, incomingFlowId?: string, parentLoop?: PlanLoopContext): void {
  const loopCollection = plan.loopCollections.find(item => item.loopObjectId === loopNode.objectId);
  const startNode = loopCollection?.startNodeId ? nodeById(plan).get(loopCollection.startNodeId) : undefined;
  const iterator = plan.loopVariables.find(item => item.scope.loopObjectId === loopNode.objectId && item.source.kind === "loopIterator");
  const iterations = Math.max(0, Math.min(input.options?.loopIterations ?? 2, 50));
  for (let index = 0; index < iterations; index += 1) {
    const loopContext: PlanLoopContext = {
      loopObjectId: loopNode.objectId,
      index,
      iteratorVariableName: iterator?.name,
      iteratorValuePreview: iterator?.name ? `${iterator.name}[${index}]` : `iteration ${index}`,
    };
    if (iterator) {
      state.values[iterator.name] = { name: iterator.name, type: iterator.dataType, valuePreview: `${iterator.name}[${index}]`, rawValue: { index }, source: "loopIterator" };
    }
    state.values.$currentIndex = { name: "$currentIndex", type: { kind: "integer" }, valuePreview: String(index), rawValue: index, source: "system" };
    pushPlanFrame(plan, state, loopNode, { incomingFlowId, status: "success", loop: parentLoop, output: { currentIndex: index, iteratorVariableName: iterator?.name }, message: `Loop iteration ${index}` });
    const signal = executePlanFromNode(plan, input, state, startNode, undefined, loopContext);
    if (state.error || signal === "break") {
      break;
    }
  }
  if (iterator) {
    delete state.values[iterator.name];
  }
  delete state.values.$currentIndex;
}

function selectPlanOutgoingFlow(plan: MicroflowExecutionPlan, input: MockRunExecutionPlanInput, state: PlanRunState, node: MicroflowExecutionNode): {
  flow?: MicroflowExecutionFlow;
  selectedCaseValue?: MicroflowTraceFrame["selectedCaseValue"];
  error?: MicroflowRuntimeError;
  output?: Record<string, unknown>;
  message?: string;
} {
  const action = node.p0ActionRuntime;
  if (node.kind === "exclusiveSplit") {
    const result = selectDecisionFlowFromPlan(plan, node, input.options);
    return result.flow
      ? { flow: result.flow, selectedCaseValue: result.selectedCaseValue, output: { selectedCaseValue: result.selectedCaseValue } }
      : { error: runtimeError("RUNTIME_INVALID_CASE", "Decision branch case was not found.", node.objectId) };
  }
  if (node.kind === "inheritanceSplit") {
    const result = selectObjectTypeFlowFromPlan(plan, node, input.options);
    return result.flow
      ? { flow: result.flow, selectedCaseValue: result.selectedCaseValue, output: { selectedCaseValue: result.selectedCaseValue } }
      : { error: runtimeError("RUNTIME_INVALID_CASE", "Object type branch case was not found.", node.objectId) };
  }
  if (action?.actionKind === "restCall" && input.options?.simulateRestError) {
    const error = runtimeError("RUNTIME_REST_CALL_FAILED", "Mock REST call failed by test option simulateRestError.", node.objectId, node.actionId);
    state.values.$latestError = { name: "$latestError", type: { kind: "object", entityQualifiedName: "System.Error" }, valuePreview: error.message, rawValue: error, source: "errorContext" };
    state.values.$latestHttpResponse = { name: "$latestHttpResponse", type: { kind: "json" }, valuePreview: "{ status: 500 }", rawValue: { status: 500, body: "mock error" }, source: "restResponse" };
    if (node.errorHandling?.errorHandlingType === "continue") {
      state.logs.push(log(state, "warning", node.objectId, node.actionId, "REST error was ignored by continue error handling."));
      return { flow: nextNormalFlow(plan, node.objectId), error, output: { continuedAfterError: true } };
    }
    if (node.errorHandling?.errorHandlingType === "rollback") {
      return { error: { ...error, code: "RUNTIME_TRANSACTION_ROLLED_BACK", message: `${error.message} Transaction rolled back.` }, output: { transaction: "rolledBack" } };
    }
    const errorFlow = plan.errorHandlerFlows.find(flow => flow.originObjectId === node.objectId);
    return errorFlow ? { flow: errorFlow, error, output: { errorHandled: true } } : { error };
  }
  return { flow: nextNormalFlow(plan, node.objectId), output: planActionOutput(node) };
}

function applyPlanActionEffects(plan: MicroflowExecutionPlan, input: MockRunExecutionPlanInput, state: PlanRunState, node: MicroflowExecutionNode, error?: MicroflowRuntimeError): void {
  const action = node.p0ActionRuntime;
  if (!action || error) {
    return;
  }
  switch (action.actionKind) {
    case "retrieve": {
      const config = action.config;
      const source = config.retrieveSource;
      const isList = source.kind === "database" && source.range.kind === "all";
      const entity = source.kind === "database" ? source.entityQualifiedName ?? "Mock.Entity" : "Mock.Association";
      state.values[config.outputVariableName] = { name: config.outputVariableName, type: isList ? { kind: "list", itemType: { kind: "object", entityQualifiedName: entity } } : { kind: "object", entityQualifiedName: entity }, valuePreview: isList ? "[mock object, mock object]" : "{ mock: true }", rawValue: isList ? [{ id: 1 }, { id: 2 }] : { id: 1 }, source: "retrieve" };
      break;
    }
    case "createObject": {
      const config = action.config;
      state.values[config.outputVariableName] = { name: config.outputVariableName, type: { kind: "object", entityQualifiedName: config.entityQualifiedName }, valuePreview: `{ ${config.entityQualifiedName} }`, rawValue: { entity: config.entityQualifiedName, memberChanges: config.memberChanges }, source: "createObject" };
      break;
    }
    case "createVariable": {
      const config = action.config;
      const initialRaw = config.initialValue?.raw;
      state.values[config.variableName] = { name: config.variableName, type: config.dataType, valuePreview: initialRaw || previewForType(config.dataType), rawValue: initialRaw, source: "createVariable" };
      break;
    }
    case "changeVariable": {
      const config = action.config;
      state.values[config.targetVariableName] = { name: config.targetVariableName, type: state.values[config.targetVariableName]?.type ?? { kind: "unknown", reason: "mock changeVariable" }, valuePreview: config.newValueExpression.raw, rawValue: config.newValueExpression.raw, source: "changeVariable" };
      break;
    }
    case "restCall": {
      const config = action.config;
      if (config.response.handling.kind !== "ignore") {
        state.values[config.response.handling.outputVariableName] = { name: config.response.handling.outputVariableName, type: config.response.handling.kind === "string" ? { kind: "string" } : { kind: "json" }, valuePreview: config.response.handling.kind === "string" ? "mock response" : "{ ok: true }", rawValue: config.response.handling.kind === "string" ? "mock response" : { ok: true }, source: "restResponse" };
      }
      if (config.response.statusCodeVariableName) {
        state.values[config.response.statusCodeVariableName] = { name: config.response.statusCodeVariableName, type: { kind: "integer" }, valuePreview: "200", rawValue: 200, source: "restResponse" };
      }
      if (config.response.headersVariableName) {
        state.values[config.response.headersVariableName] = { name: config.response.headersVariableName, type: { kind: "json" }, valuePreview: "{ content-type: application/json }", rawValue: { "content-type": "application/json" }, source: "restResponse" };
      }
      break;
    }
    case "logMessage": {
      const config = action.config;
      state.logs.push(log(state, config.template.text ? action.config.level : "info", node.objectId, node.actionId, config.template.text || "Mock log message"));
      break;
    }
    case "commit": {
      const config = action.config;
      state.logs.push(log(state, "info", node.objectId, node.actionId, `Committed ${config.objectOrListVariableName}`));
      break;
    }
    case "delete": {
      const config = action.config;
      state.logs.push(log(state, "info", node.objectId, node.actionId, `Deleted ${config.objectOrListVariableName}`));
      break;
    }
    case "rollback": {
      const config = action.config;
      state.logs.push(log(state, "warning", node.objectId, node.actionId, `Rolled back ${config.objectOrListVariableName}`));
      break;
    }
    case "callMicroflow": {
      const config = action.config;
      if (config.returnValue.storeResult && config.returnValue.outputVariableName) {
        state.values[config.returnValue.outputVariableName] = { name: config.returnValue.outputVariableName, type: { kind: "unknown", reason: "mock callMicroflow return" }, valuePreview: "mock return", rawValue: "mock return", source: "microflowReturn" };
      }
      break;
    }
    default:
      break;
  }
}

function selectDecisionFlowFromPlan(plan: MicroflowExecutionPlan, node: MicroflowExecutionNode, options?: MicroflowTestRunOptions) {
  const outgoing = plan.decisionFlows.filter(flow => flow.originObjectId === node.objectId && flow.controlFlow === "decision").sort(byBranchOrder);
  const requestedBoolean = options?.decisionBooleanResult ?? true;
  const requestedEnum = options?.enumerationCaseValue;
  const enumSelected = requestedEnum ? outgoing.find(flow => flow.caseValues.some(value => value.kind === "enumeration" && value.value === requestedEnum)) : undefined;
  const boolSelected = outgoing.find(flow => flow.caseValues.some(value => value.kind === "boolean" && value.value === requestedBoolean));
  const fallback = enumSelected ?? boolSelected ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "empty" || value.kind === "noCase"));
  return { flow: fallback, selectedCaseValue: fallback?.caseValues[0] };
}

function selectObjectTypeFlowFromPlan(plan: MicroflowExecutionPlan, node: MicroflowExecutionNode, options?: MicroflowTestRunOptions) {
  const outgoing = plan.decisionFlows.filter(flow => flow.originObjectId === node.objectId && flow.controlFlow === "objectType").sort(byBranchOrder);
  const requested = options?.objectTypeCase;
  const selected = requested ? outgoing.find(flow => flow.caseValues.some(value => value.kind === "inheritance" && value.entityQualifiedName === requested)) : outgoing.find(flow => flow.caseValues.some(value => value.kind === "inheritance"));
  const fallback = selected ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "empty")) ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "fallback"));
  return { flow: fallback, selectedCaseValue: fallback?.caseValues[0] };
}

function nextNormalFlow(plan: MicroflowExecutionPlan, objectId: string): MicroflowExecutionFlow | undefined {
  return plan.normalFlows.filter(flow => flow.originObjectId === objectId).sort(byBranchOrder)[0];
}

function nodeById(plan: MicroflowExecutionPlan): Map<string, MicroflowExecutionNode> {
  return new Map(plan.nodes.map(node => [node.objectId, node]));
}

function byBranchOrder(left: MicroflowExecutionFlow, right: MicroflowExecutionFlow): number {
  return (left.branchOrder ?? 0) - (right.branchOrder ?? 0) || left.flowId.localeCompare(right.flowId);
}

function pushPlanFrame(plan: MicroflowExecutionPlan, state: PlanRunState, node: MicroflowExecutionNode, options: {
  incomingFlowId?: string;
  outgoingFlowId?: string;
  selectedCaseValue?: MicroflowTraceFrame["selectedCaseValue"];
  status: MicroflowTraceFrame["status"];
  output?: Record<string, unknown>;
  error?: MicroflowRuntimeError;
  loop?: PlanLoopContext;
  message?: string;
  errorHandlerVisited?: boolean;
}): void {
  const startedAt = state.startedAtMs + state.trace.length * 14;
  const endedAt = startedAt + 8;
  const frame: MicroflowTraceFrame = {
    id: `${state.runId}-${state.trace.length + 1}`,
    frameId: `${state.runId}-${state.trace.length + 1}`,
    runId: state.runId,
    objectId: node.objectId,
    nodeId: node.objectId,
    objectTitle: node.caption ?? node.kind,
    nodeTitle: node.caption ?? node.kind,
    actionId: node.actionId,
    collectionId: node.collectionId,
    incomingFlowId: options.incomingFlowId,
    outgoingFlowId: options.outgoingFlowId,
    incomingEdgeId: options.incomingFlowId,
    outgoingEdgeId: options.outgoingFlowId,
    selectedCaseValue: options.selectedCaseValue,
    loopIteration: options.loop,
    errorHandlerVisited: options.errorHandlerVisited ?? Boolean(options.outgoingFlowId && plan.errorHandlerFlows.some(flow => flow.flowId === options.outgoingFlowId)),
    status: options.status,
    startedAt: new Date(startedAt).toISOString(),
    endedAt: new Date(endedAt).toISOString(),
    durationMs: endedAt - startedAt,
    input: state.trace.length === 0 ? {} : { incomingFlowId: options.incomingFlowId },
    output: options.output,
    error: options.error,
    variablesSnapshot: { ...state.values },
    message: options.message,
  };
  state.trace.push(frame);
}

function finishPlanSession(plan: MicroflowExecutionPlan, input: MockRunExecutionPlanInput, state: PlanRunState, status: MicroflowRunSession["status"], error?: MicroflowRuntimeError): MicroflowRunSession {
  return {
    id: state.runId,
    schemaId: plan.schemaId,
    startedAt: new Date(state.startedAtMs).toISOString(),
    endedAt: new Date(state.startedAtMs + Math.max(1, state.trace.length) * 14).toISOString(),
    status,
    input: input.parameters,
    output: state.output,
    error,
    trace: state.trace,
    logs: state.logs,
    variables: state.trace.map(frame => ({ frameId: frame.id, objectId: frame.objectId, variables: Object.values(frame.variablesSnapshot ?? {}) })),
  };
}

function buildInitialPlanValues(plan: MicroflowExecutionPlan, parameters: Record<string, unknown>): Record<string, MicroflowRuntimeVariableValue> {
  const values: Record<string, MicroflowRuntimeVariableValue> = {};
  for (const parameter of plan.parameters) {
    values[parameter.name] = { name: parameter.name, type: parameter.dataType, valuePreview: previewValue(parameters[parameter.name], parameter.dataType), rawValue: parameters[parameter.name], source: "parameter" };
  }
  return values;
}

function runtimeError(code: MicroflowRuntimeError["code"], message: string, objectId?: string, actionId?: string): MicroflowRuntimeError {
  return { code, message, objectId, actionId };
}

function previewValue(value: unknown, dataType: MicroflowRuntimeVariableValue["type"]): string {
  if (value === undefined || value === null || value === "") {
    return previewForType(dataType);
  }
  return typeof value === "object" ? JSON.stringify(value) : String(value);
}

function previewForType(dataType: MicroflowRuntimeVariableValue["type"]): string {
  if (dataType.kind === "object") {
    return `{ ${dataType.entityQualifiedName} }`;
  }
  if (dataType.kind === "list") {
    return "[]";
  }
  return dataType.kind;
}

function planActionOutput(node: MicroflowExecutionNode): Record<string, unknown> {
  return node.p0ActionRuntime ? { actionKind: node.p0ActionRuntime.actionKind } : { objectKind: node.kind };
}

function log(state: PlanRunState, level: MicroflowRuntimeLog["level"], objectId: string, actionId: string | undefined, message: string): MicroflowRuntimeLog {
  return {
    id: `${state.runId}-log-${state.logs.length + 1}`,
    timestamp: new Date(state.startedAtMs + state.logs.length * 9).toISOString(),
    level,
    objectId,
    actionId,
    message,
  };
}
