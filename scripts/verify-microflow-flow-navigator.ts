const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";

type Json = Record<string, unknown>;

const headers = {
  Accept: "application/json",
  "Content-Type": "application/json",
  "X-Workspace-Id": workspaceId,
  "X-Tenant-Id": tenantId,
  "X-User-Id": userId,
};

function makeId(prefix: string): string {
  return `${prefix}${Date.now()}${Math.random().toString(36).slice(2, 8)}`;
}

async function api(method: string, path: string, body?: unknown, expectSuccess = true): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (!envelope || typeof envelope.success !== "boolean") {
    throw new Error(`${method} ${path} did not return MicroflowApiResponse<T>`);
  }
  if (expectSuccess && (!response.ok || envelope.success !== true)) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${envelope.error?.code ?? ""}`);
  }
  return envelope;
}

function baseSchema(name: string, objects: Json[], flows: Json[]): Json {
  const id = makeId(name);
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    name,
    displayName: name,
    moduleId: "verify-module",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root-collection",
      objects,
      flows,
    },
    flows: [],
  };
}

function startEndSchema(): Json {
  return baseSchema("NavigatorStartEnd", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-end", kind: "sequence", originObjectId: "start", destinationObjectId: "end" },
  ]);
}

function actionSchema(actionKind = "retrieve"): Json {
  return baseSchema("NavigatorAction", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    {
      id: "action",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      action: { id: "action-1", kind: actionKind, officialType: `Microflows$${actionKind}` },
    },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-action", kind: "sequence", originObjectId: "start", destinationObjectId: "action" },
    { id: "f-action-end", kind: "sequence", originObjectId: "action", destinationObjectId: "end" },
  ]);
}

function booleanDecisionSchema(): Json {
  return baseSchema("NavigatorBooleanDecision", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "decision", kind: "exclusiveSplit", officialType: "Microflows$ExclusiveSplit" },
    { id: "trueEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "falseEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-decision", kind: "sequence", originObjectId: "start", destinationObjectId: "decision" },
    { id: "f-decision-true", kind: "sequence", originObjectId: "decision", destinationObjectId: "trueEnd", editor: { edgeKind: "decisionCondition", branchOrder: 0 }, caseValues: [{ kind: "boolean", value: true }] },
    { id: "f-decision-false", kind: "sequence", originObjectId: "decision", destinationObjectId: "falseEnd", editor: { edgeKind: "decisionCondition", branchOrder: 1 }, caseValues: [{ kind: "boolean", value: false }] },
  ]);
}

function enumerationDecisionSchema(): Json {
  return baseSchema("NavigatorEnumerationDecision", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "decision", kind: "exclusiveSplit", officialType: "Microflows$ExclusiveSplit" },
    { id: "approvedEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "rejectedEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-decision", kind: "sequence", originObjectId: "start", destinationObjectId: "decision" },
    { id: "f-decision-approved", kind: "sequence", originObjectId: "decision", destinationObjectId: "approvedEnd", editor: { edgeKind: "decisionCondition", branchOrder: 0 }, caseValues: [{ kind: "enumeration", persistedValue: "Approved" }] },
    { id: "f-decision-rejected", kind: "sequence", originObjectId: "decision", destinationObjectId: "rejectedEnd", editor: { edgeKind: "decisionCondition", branchOrder: 1 }, caseValues: [{ kind: "enumeration", persistedValue: "Rejected" }] },
  ]);
}

function objectTypeSchema(): Json {
  return baseSchema("NavigatorObjectType", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "split", kind: "inheritanceSplit", officialType: "Microflows$InheritanceSplit" },
    { id: "profEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "studentEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "fallbackEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-split", kind: "sequence", originObjectId: "start", destinationObjectId: "split" },
    { id: "f-split-prof", kind: "sequence", originObjectId: "split", destinationObjectId: "profEnd", editor: { edgeKind: "objectTypeCondition", branchOrder: 0 }, caseValues: [{ kind: "inheritance", entityQualifiedName: "University.Professor" }] },
    { id: "f-split-student", kind: "sequence", originObjectId: "split", destinationObjectId: "studentEnd", editor: { edgeKind: "objectTypeCondition", branchOrder: 1 }, caseValues: [{ kind: "inheritance", entityQualifiedName: "University.Student" }] },
    { id: "f-split-fallback", kind: "sequence", originObjectId: "split", destinationObjectId: "fallbackEnd", editor: { edgeKind: "objectTypeCondition", branchOrder: 2 }, caseValues: [{ kind: "fallback" }] },
  ]);
}

function mergeSchema(): Json {
  return baseSchema("NavigatorMerge", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "action", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "log", kind: "logMessage", officialType: "Microflows$LogMessageAction" } },
    { id: "merge", kind: "exclusiveMerge", officialType: "Microflows$ExclusiveMerge" },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "note", kind: "annotation", officialType: "Microflows$Annotation" },
  ], [
    { id: "f-start-action", kind: "sequence", originObjectId: "start", destinationObjectId: "action" },
    { id: "f-action-merge", kind: "sequence", originObjectId: "action", destinationObjectId: "merge" },
    { id: "f-merge-end", kind: "sequence", originObjectId: "merge", destinationObjectId: "end" },
    { id: "f-note", kind: "annotation", edgeKind: "annotation", originObjectId: "note", destinationObjectId: "merge" },
  ]);
}

function restErrorSchema(destination = "handledEnd"): Json {
  return baseSchema("NavigatorRestError", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "rest", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "rest-action", kind: "restCall", officialType: "Microflows$RestCallAction" } },
    { id: "handledEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "errorEvent", kind: "errorEvent", officialType: "Microflows$ErrorEvent" },
  ], [
    { id: "f-start-rest", kind: "sequence", originObjectId: "start", destinationObjectId: "rest" },
    { id: "f-rest-success", kind: "sequence", originObjectId: "rest", destinationObjectId: "handledEnd" },
    { id: "f-rest-error", kind: "sequence", originObjectId: "rest", destinationObjectId: destination, isErrorHandler: true, editor: { edgeKind: "errorHandler" } },
  ]);
}

function loopSchema(kind: "normal" | "break" | "continue" = "normal"): Json {
  const innerNode = kind === "normal"
    ? { id: "loop-log", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "loop-log-action", kind: "logMessage", officialType: "Microflows$LogMessageAction", level: "info", template: { text: "loop" } } }
    : { id: `loop-${kind}`, kind: `${kind}Event`, officialType: `Microflows$${kind[0].toUpperCase()}${kind.slice(1)}Event` };
  const innerObjects = kind === "normal"
    ? [innerNode, { id: "loop-continue", kind: "continueEvent", officialType: "Microflows$ContinueEvent" }]
    : [innerNode];
  return baseSchema("NavigatorLoop", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    {
      id: "loop",
      kind: "loopedActivity",
      officialType: "Microflows$LoopedActivity",
      loopSource: { kind: "whileCondition", expression: "true", maxIterations: 10 },
      objectCollection: {
        id: "loop-collection",
        objects: innerObjects,
        flows: kind === "normal"
          ? [
            { id: "lf-log-continue", kind: "sequence", originObjectId: "loop-log", destinationObjectId: "loop-continue" },
          ]
          : [
          ],
      },
    },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-loop", kind: "sequence", originObjectId: "start", destinationObjectId: "loop" },
    { id: "f-loop-end", kind: "sequence", originObjectId: "loop", destinationObjectId: "end" },
  ]);
}

async function navigate(schema: Json, options: Json = {}): Promise<Json> {
  return (await api("POST", "/api/microflows/runtime/navigate", { schema, options })).data as Json;
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function steps(result: Json): Json[] {
  return result.steps as Json[];
}

function assertNoFlowGramJson(result: Json): void {
  const text = JSON.stringify(result);
  assert(!text.includes("\"edges\""), "NavigationResult must not contain FlowGram edges JSON");
  assert(!text.includes("\"workflowJson\""), "NavigationResult must not contain WorkflowJSON");
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const startEnd = await navigate(startEndSchema());
  assert(startEnd.status === "success" && startEnd.terminalNodeId === "end", "Start -> End should succeed");

  const missingStart = await navigate(baseSchema("NavigatorMissingStart", [{ id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" }], []));
  assert(missingStart.status === "failed" && (missingStart.error as Json).code === "RUNTIME_START_NOT_FOUND", "missing StartEvent should fail");

  const action = await navigate(actionSchema("retrieve"));
  assert(action.status === "success" && steps(action).some(step => step.objectId === "action" && step.status === "success"), "P0 action placeholder should succeed");

  const unsupported = await navigate(actionSchema("unknownAction"));
  assert(unsupported.status === "failed" && (unsupported.error as Json).code === "RUNTIME_UNSUPPORTED_ACTION", "unsupported action should fail");

  const modeledStop = await navigate(actionSchema("showPage"), { stopOnUnsupported: true });
  assert(modeledStop.status === "success" && steps(modeledStop).some(step => step.objectId === "action" && step.status === "success" && step.output?.runtimeCommands), "runtimeCommand action should succeed with command output");

  const modeledSkip = await navigate(actionSchema("showPage"), { stopOnUnsupported: false });
  assert(modeledSkip.status === "success" && steps(modeledSkip).some(step => step.objectId === "action" && step.status === "success" && step.output?.runtimeCommands), "runtimeCommand action should not be skipped");

  const booleanTrue = await navigate(booleanDecisionSchema(), { decisionBooleanResult: true });
  assert(booleanTrue.terminalNodeId === "trueEnd", "boolean true should select true branch");

  const booleanFalse = await navigate(booleanDecisionSchema(), { decisionBooleanResult: false });
  assert(booleanFalse.terminalNodeId === "falseEnd", "boolean false should select false branch");

  const enumeration = await navigate(enumerationDecisionSchema(), { enumerationCaseValue: "Rejected" });
  assert(enumeration.terminalNodeId === "rejectedEnd", "enumeration option should select matching branch");

  const objectType = await navigate(objectTypeSchema(), { objectTypeCase: "University.Professor" });
  assert(objectType.terminalNodeId === "profEnd", "objectType option should select matching branch");

  const objectFallback = await navigate(objectTypeSchema());
  assert(objectFallback.terminalNodeId === "fallbackEnd", "objectType should choose fallback without option");

  const invalidCase = await navigate(objectTypeSchema(), { objectTypeCase: "University.Unknown" });
  assert(invalidCase.status === "failed" && (invalidCase.error as Json).code === "RUNTIME_INVALID_CASE", "invalid object type case should fail");

  const merge = await navigate(mergeSchema());
  assert(merge.status === "success" && steps(merge).some(step => step.objectId === "merge"), "merge arrival should continue");
  assert(!steps(merge).some(step => step.objectId === "note"), "AnnotationFlow should not affect navigation");

  const restHandled = await navigate(restErrorSchema("handledEnd"), { simulateRestError: true });
  assert(restHandled.status === "success" && steps(restHandled).some(step => step.outgoingFlowId === "f-rest-error"), "RestCall simulated error should enter handler");

  const restMissingHandlerSchema = restErrorSchema("handledEnd");
  const root = restMissingHandlerSchema.objectCollection as Json;
  root.flows = (root.flows as Json[]).filter(flow => flow.id !== "f-rest-error");
  const restMissing = await navigate(restMissingHandlerSchema, { simulateRestError: true });
  assert(restMissing.status === "failed" && (restMissing.error as Json).code === "RUNTIME_REST_CALL_FAILED", "RestCall simulated error without handler should fail");

  const restErrorEvent = await navigate(restErrorSchema("errorEvent"), { simulateRestError: true });
  assert(restErrorEvent.status === "failed" && (restErrorEvent.error as Json).code === "RUNTIME_ERROR_EVENT_REACHED", "handler path to ErrorEvent should fail");

  // Deep Loop / Break / Continue execution is covered by verify-microflow-loop-runtime.
  const maxSteps = await navigate(actionSchema("retrieve"), { maxSteps: 1 });
  assert(maxSteps.status === "maxStepsExceeded" && (maxSteps.error as Json).code === "RUNTIME_MAX_STEPS_EXCEEDED", "maxSteps should stop navigation");

  assert(Array.isArray(startEnd.traceFrames) && (startEnd.traceFrames as Json[]).length === steps(startEnd).length, "NavigationResult should include trace skeleton");
  assertNoFlowGramJson(startEnd);

  console.log("PASS verify-microflow-flow-navigator");
}

run().catch(error => {
  console.error("FAIL verify-microflow-flow-navigator");
  console.error(error);
  process.exit(1);
});
