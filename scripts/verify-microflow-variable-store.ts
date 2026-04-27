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

async function api(method: string, path: string, body?: unknown): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (!envelope?.success || !response.ok) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return envelope.data as Json;
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function baseSchema(name: string, objects: Json[], flows: Json[], parameters: Json[] = []): Json {
  const id = makeId(name);
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    name,
    displayName: name,
    moduleId: "verify-module",
    parameters,
    returnType: { kind: "void" },
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
  };
}

function variableSchema(): Json {
  return baseSchema(
    "VariableStoreAction",
    [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      {
        id: "retrieve",
        kind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        action: {
          id: "retrieve-action",
          kind: "retrieve",
          officialType: "Microflows$RetrieveAction",
          outputVariableName: "orders",
          retrieveSource: { entityQualifiedName: "Sales.Order", mode: "databaseAll" },
        },
      },
      { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
    ],
    [
      { id: "f-start-retrieve", kind: "sequence", originObjectId: "start", destinationObjectId: "retrieve" },
      { id: "f-retrieve-end", kind: "sequence", originObjectId: "retrieve", destinationObjectId: "end" },
    ],
    [{ id: "p-customer", name: "customerId", required: true, dataType: { kind: "string" }, type: { kind: "string" } }]
  );
}

function loopSchema(): Json {
  return baseSchema(
    "VariableStoreLoop",
    [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      {
        id: "loop",
        kind: "loopedActivity",
        officialType: "Microflows$LoopedActivity",
        objectCollection: {
          id: "loop-collection",
          objects: [
            { id: "loop-start", kind: "startEvent", officialType: "Microflows$StartEvent" },
            { id: "loop-log", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "loop-log-action", kind: "logMessage", officialType: "Microflows$LogMessageAction" } },
            { id: "loop-end", kind: "endEvent", officialType: "Microflows$EndEvent" },
          ],
          flows: [
            { id: "lf-start-log", kind: "sequence", originObjectId: "loop-start", destinationObjectId: "loop-log" },
            { id: "lf-log-end", kind: "sequence", originObjectId: "loop-log", destinationObjectId: "loop-end" },
          ],
        },
      },
      { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
    ],
    [
      { id: "f-start-loop", kind: "sequence", originObjectId: "start", destinationObjectId: "loop" },
      { id: "f-loop-end", kind: "sequence", originObjectId: "loop", destinationObjectId: "end" },
    ]
  );
}

function restErrorSchema(): Json {
  return baseSchema(
    "VariableStoreRestError",
    [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      { id: "rest", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "rest-action", kind: "restCall", officialType: "Microflows$RestCallAction" } },
      { id: "handledEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
      { id: "normalEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    ],
    [
      { id: "f-start-rest", kind: "sequence", originObjectId: "start", destinationObjectId: "rest" },
      { id: "f-rest-normal", kind: "sequence", originObjectId: "rest", destinationObjectId: "normalEnd" },
      { id: "f-rest-error", kind: "sequence", originObjectId: "rest", destinationObjectId: "handledEnd", isErrorHandler: true, editor: { edgeKind: "errorHandler" } },
    ]
  );
}

async function navigate(schema: Json, options: Json = {}): Promise<Json> {
  return api("POST", "/api/microflows/runtime/navigate", { schema, options: { includeVariableSnapshots: true, ...options } });
}

function frames(result: Json): Json[] {
  return (result.traceFrames ?? result.steps) as Json[];
}

function vars(frame: Json): Record<string, Json> {
  return (frame.variablesSnapshot ?? {}) as Record<string, Json>;
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const actionResult = await navigate(variableSchema());
  const actionFrames = frames(actionResult);
  const retrieveFrame = actionFrames.find(frame => frame.objectId === "retrieve")!;
  assert(vars(retrieveFrame).customerId, "parameter variable should be visible");
  assert(vars(retrieveFrame).$currentUser, "$currentUser should be visible");
  assert(vars(retrieveFrame).orders, "Retrieve output variable should be written");
  assert(String(vars(retrieveFrame).orders.valuePreview).length > 0, "snapshot should contain valuePreview");
  assert(!JSON.stringify(retrieveFrame).includes("objectCollection"), "variable snapshot must not leak FlowGram JSON");
  assert((actionResult.diagnostics as Json).items, "diagnostics should be present");

  const loopResult = await navigate(loopSchema(), { loopIterations: 2 });
  const loopFrame = frames(loopResult).find(frame => frame.objectId === "loop-log")!;
  assert(vars(loopFrame).$iterator, "loop iterator should be visible inside loop");
  assert(vars(loopFrame).$currentIndex, "$currentIndex should be visible inside loop");
  const finalLoopFrame = frames(loopResult).at(-1)!;
  assert(!vars(finalLoopFrame).$iterator && !vars(finalLoopFrame).$currentIndex, "loop variables should not leak after loop scope");

  const errorResult = await navigate(restErrorSchema(), { simulateRestError: true });
  const handledFrame = frames(errorResult).find(frame => frame.objectId === "handledEnd")!;
  assert(vars(handledFrame).$latestError, "$latestError should be visible in error handler");
  assert(vars(handledFrame).$latestHttpResponse, "$latestHttpResponse should be visible in REST error handler");

  console.log("verify-microflow-variable-store: PASS");
}

run().catch(error => {
  console.error("verify-microflow-variable-store: FAIL");
  console.error(error);
  process.exitCode = 1;
});
