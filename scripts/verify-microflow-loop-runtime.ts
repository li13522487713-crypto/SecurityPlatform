const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";

type Json = Record<string, any>;

const headers = {
  Accept: "application/json",
  "Content-Type": "application/json",
  "X-Workspace-Id": workspaceId,
  "X-Tenant-Id": tenantId,
  "X-User-Id": userId,
};

let resourceId = process.env.MICROFLOW_VERIFY_RESOURCE_ID ?? "";

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

async function api(method: string, path: string, body?: unknown, expectSuccess = true): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (expectSuccess && (!response.ok || envelope?.success !== true)) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return envelope?.data as Json;
}

async function ensureResource(): Promise<void> {
  if (resourceId) {
    return;
  }

  const created = await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name: `RuntimeRound55${Date.now()}`,
      displayName: "Runtime Round 55 Loop Verify",
      description: "Created by verify-microflow-loop-runtime.ts",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "runtime-loop"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  resourceId = String(created.id ?? "");
  assert(resourceId, "created resource id should be returned");
}

function schema(name: string, objects: Json[], flows: Json[], parameters: Json[] = []): Json {
  return {
    schemaVersion: "1.0.0",
    id: `verify-${name}`,
    name,
    moduleId: "verify-module",
    parameters,
    returnType: { kind: "void" },
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
  };
}

function listParam(): Json {
  return {
    id: "param-items",
    name: "items",
      type: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } },
    required: true,
  };
}

function iterableLoopSchema(innerObjects: Json[], innerFlows: Json[], loopSource: Json = {}): Json {
  return schema("LoopIterable", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    {
      id: "loop",
      kind: "loopedActivity",
      officialType: "Microflows$LoopedActivity",
      loopSource: {
        kind: "iterableList",
        listVariableName: "items",
        iteratorVariableName: "item",
        ...loopSource,
      },
      objectCollection: {
        id: "loop-collection",
        objects: innerObjects,
        flows: innerFlows,
      },
    },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-loop", kind: "sequence", originObjectId: "start", destinationObjectId: "loop" },
    { id: "f-loop-end", kind: "sequence", originObjectId: "loop", destinationObjectId: "end" },
  ], [listParam()]);
}

function continueBody(): { objects: Json[]; flows: Json[] } {
  return {
    objects: [
      { id: "loop-log", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "loop-log-action", kind: "logMessage", level: "info", template: { text: "loop item" } } },
      { id: "loop-continue", kind: "continueEvent", officialType: "Microflows$ContinueEvent" },
    ],
    flows: [
      { id: "lf-log-continue", kind: "sequence", originObjectId: "loop-log", destinationObjectId: "loop-continue" },
    ],
  };
}

function breakBody(): { objects: Json[]; flows: Json[] } {
  return {
    objects: [
      { id: "loop-break", kind: "breakEvent", officialType: "Microflows$BreakEvent" },
    ],
    flows: [],
  };
}

async function testRun(testSchema: Json, input: Json, options: Json = {}): Promise<Json> {
  const result = await api("POST", `/api/microflows/${resourceId}/test-run`, { schema: testSchema, input, options });
  return result.session as Json;
}

function trace(session: Json): Json[] {
  return session.trace as Json[];
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");
  await ensureResource();

  const body = continueBody();
  const iterable = await testRun(
    iterableLoopSchema(body.objects, body.flows),
    { items: [{ id: "a" }, { id: "b" }, { id: "c" }] },
  );
  assert(iterable.status === "success", "iterable list with 3 items should succeed");
  const iterableFrames = trace(iterable).filter((frame) => frame.loopIteration);
  assert(iterableFrames.filter((frame) => frame.objectId === "loop-log").length === 3, "loop body action should run 3 times");
  assert(iterableFrames.some((frame) => frame.loopIteration?.index === 0 && frame.variablesSnapshot?.item), "iterator should be visible inside loop");
  assert(iterableFrames.some((frame) => frame.loopIteration?.index === 2 && frame.variablesSnapshot?.$currentIndex?.valuePreview === "2"), "$currentIndex should reach 2");
  assert(!trace(iterable).find((frame) => frame.objectId === "end")?.variablesSnapshot?.item, "iterator should not leak after loop");

  const empty = await testRun(iterableLoopSchema(body.objects, body.flows), { items: [] });
  assert(empty.status === "success", "empty list should skip loop and continue");
  assert(!trace(empty).some((frame) => frame.objectId === "loop-log"), "empty list should not execute body");

  const br = breakBody();
  const breakRun = await testRun(iterableLoopSchema(br.objects, br.flows), { items: [{ id: "a" }, { id: "b" }, { id: "c" }] });
  assert(breakRun.status === "success", "break inside loop should complete outer flow");
  assert(trace(breakRun).filter((frame) => frame.objectId === "loop-break").length === 1, "break should stop current loop");
  assert(trace(breakRun).some((frame) => frame.message === "Break loop." || frame.loopIteration?.controlSignal === "break"), "break trace should be explicit");

  const whileFalse = await api("POST", "/api/microflows/runtime/navigate", {
    schema: schema("LoopWhileFalse", [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      {
        id: "loop",
        kind: "loopedActivity",
        officialType: "Microflows$LoopedActivity",
        loopSource: { kind: "whileCondition", expression: "false", maxIterations: 2 },
        objectCollection: { id: "loop-collection", objects: body.objects, flows: body.flows },
      },
      { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
    ], [
      { id: "f-start-loop", kind: "sequence", originObjectId: "start", destinationObjectId: "loop" },
      { id: "f-loop-end", kind: "sequence", originObjectId: "loop", destinationObjectId: "end" },
    ]),
    options: { includeVariableSnapshots: true },
  });
  assert(whileFalse.status === "success", "while false should skip loop");
  assert(!JSON.stringify(whileFalse).includes("FlowGram"), "loop trace must not contain FlowGram JSON");

  console.log("verify-microflow-loop-runtime: PASS");
}

run().catch((error) => {
  console.error("verify-microflow-loop-runtime: FAIL");
  console.error(error);
  process.exitCode = 1;
});
