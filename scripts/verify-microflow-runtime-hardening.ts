import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const baseUrl = (process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002").replace(/\/+$/u, "");
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";
const runPrefix = process.env.MICROFLOW_ROUND60_PREFIX ?? "R60_E2E_";

type Json = Record<string, any>;

const headers = {
  Accept: "application/json",
  "Content-Type": "application/json",
  "X-Workspace-Id": workspaceId,
  "X-Tenant-Id": tenantId,
  "X-User-Id": userId,
};

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function read(relativePath: string): string {
  return readFileSync(join(root, relativePath), "utf8");
}

function assertIncludes(file: string, needles: string[]): void {
  const content = read(file);
  for (const needle of needles) {
    assert(content.includes(needle), `${file} missing ${needle}`);
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
  if (typeof envelope?.success !== "boolean") {
    throw new Error(`${method} ${path} did not return MicroflowApiResponse<T>`);
  }
  if (expectSuccess && (!response.ok || envelope.success !== true)) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return expectSuccess ? envelope.data : envelope;
}

async function createResource(name: string): Promise<string> {
  const created = await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name,
      displayName: name,
      description: "Created by verify-microflow-runtime-hardening.ts",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "round60", "hardening"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  assert(created.id, "created resource id should exist");
  return String(created.id);
}

function schema(name: string, objects: Json[], flows: Json[], parameters: Json[] = []): Json {
  return {
    schemaVersion: "1.0.0",
    id: `${runPrefix}${name}`,
    name,
    displayName: name,
    moduleId: "verify-module",
    moduleName: "Verify",
    parameters,
    returnType: { kind: "void" },
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
  };
}

function chain(objects: Json[]): Json[] {
  return objects.slice(0, -1).map((object, index) => ({
    id: `f-${String(object.id)}-${String(objects[index + 1].id)}`,
    kind: "sequence",
    originObjectId: object.id,
    destinationObjectId: objects[index + 1].id,
  }));
}

async function testRun(resourceId: string, testSchema: Json, input: Json = {}, options: Json = {}): Promise<Json> {
  const result = await api("POST", `/api/microflows/${resourceId}/test-run`, { schema: testSchema, input, options });
  return result.session as Json;
}

function assertRuntimeError(session: Json, expectedCodes: string[], label: string): void {
  const actual = String(session.error?.code ?? "");
  assert(session.status === "failed", `${label}: session should fail`);
  assert(expectedCodes.includes(actual), `${label}: expected ${expectedCodes.join(", ")}, got ${actual}`);
}

async function runApiHardening(): Promise<void> {
  await api("GET", "/api/microflows/health");
  const resourceId = await createResource(`${runPrefix}Hardening_${Date.now()}`);

  const successSchema = schema("CancelCompleted", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "log", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "log-1", kind: "logMessage", level: "info", template: { text: "round60" }, includeTraceId: true } },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-log", kind: "sequence", originObjectId: "start", destinationObjectId: "log" },
    { id: "f-log-end", kind: "sequence", originObjectId: "log", destinationObjectId: "end" },
  ]);

  const completed = await testRun(resourceId, successSchema, {}, { maxSteps: 20 });
  assert(completed.status === "success", "baseline run should succeed before cancel check");
  assert(Array.isArray(completed.trace) && completed.trace.length > 0, "baseline run should contain trace");
  assert(Array.isArray(completed.logs) && completed.logs.length > 0, "baseline run should contain logs");
  const persisted = await api("GET", `/api/microflows/runs/${completed.id}`);
  const trace = await api("GET", `/api/microflows/runs/${completed.id}/trace`);
  assert(persisted.id === completed.id, "RunSession should be persisted");
  assert(Array.isArray(trace.trace) && Array.isArray(trace.logs), "TraceFrame/RunLog query should return arrays");
  const cancelled = await api("POST", `/api/microflows/runs/${completed.id}/cancel`);
  assert(["cancelled", "success", "failed"].includes(String(cancelled.status)), "cancel endpoint should preserve a terminal status or return cancelled");

  const maxStepsSchema = schema("MaxSteps", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "a1", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "a1-action", kind: "logMessage", level: "info", template: { text: "a1" } } },
    { id: "a2", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "a2-action", kind: "logMessage", level: "info", template: { text: "a2" } } },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], chain([{ id: "start" }, { id: "a1" }, { id: "a2" }, { id: "end" }]));
  assertRuntimeError(await testRun(resourceId, maxStepsSchema, {}, { maxSteps: 1 }), ["RUNTIME_MAX_STEPS_EXCEEDED"], "maxSteps");

  const loopSchema = schema("MaxIterations", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    {
      id: "loop",
      kind: "loopedActivity",
      officialType: "Microflows$LoopedActivity",
      loopSource: { kind: "whileCondition", expression: "true", maxIterations: 1 },
      objectCollection: {
        id: "loop-collection",
        objects: [
          { id: "loop-continue", kind: "continueEvent", officialType: "Microflows$ContinueEvent" },
        ],
        flows: [],
      },
    },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-loop", kind: "sequence", originObjectId: "start", destinationObjectId: "loop" },
    { id: "f-loop-end", kind: "sequence", originObjectId: "loop", destinationObjectId: "end" },
  ]);
  assertRuntimeError(await testRun(resourceId, loopSchema), ["RUNTIME_LOOP_MAX_ITERATIONS_EXCEEDED"], "maxIterations");

  const restBlockedSchema = schema("RestBlocked", [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "rest", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "rest-1", kind: "restCall", request: { method: "GET", urlExpression: "$url" }, response: { handling: { kind: "ignore" } }, timeoutSeconds: 1 } },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-rest", kind: "sequence", originObjectId: "start", destinationObjectId: "rest" },
    { id: "f-rest-end", kind: "sequence", originObjectId: "rest", destinationObjectId: "end" },
  ], [{ id: "p-url", name: "url", type: { kind: "string" }, required: true }]);
  assertRuntimeError(
    await testRun(resourceId, restBlockedSchema, { url: "http://127.0.0.1:1/private" }, { allowRealHttp: true }),
    ["RUNTIME_REST_PRIVATE_NETWORK_BLOCKED", "RUNTIME_REST_URL_BLOCKED", "RUNTIME_REST_DENIED_HOST", "RUNTIME_REST_UNSUPPORTED_SCHEME"],
    "rest SSRF policy",
  );
}

function runStaticHardening(): void {
  assertIncludes("src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs", [
    "RuntimeMaxStepsExceeded",
    "RuntimeLoopMaxIterationsExceeded",
    "RuntimeRestTimeout",
    "RuntimeCancelled",
    "RuntimeCallStackOverflow",
    "RuntimeErrorHandlerMaxDepthExceeded",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Actions/Http/MicroflowRuntimeHttpClient.cs", [
    "CancelAfter",
    "MaxResponseBytes",
    "Truncated",
    "RuntimeRestTimeout",
    "RuntimeCancelled",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Calls/MicroflowCallStackService.cs", [
    "maxCallDepth",
    "RuntimeCallStackOverflow",
    "MaxDepthExceeded",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/ErrorHandling/MicroflowErrorHandlingService.cs", [
    "DefaultMaxErrorHandlingDepth",
    "RuntimeErrorHandlerMaxDepthExceeded",
  ]);
}

async function main(): Promise<void> {
  try {
    runStaticHardening();
    await runApiHardening();
    console.log("verify-microflow-runtime-hardening: PASS");
  } catch (error) {
    console.error("verify-microflow-runtime-hardening: FAIL");
    console.error(error);
    process.exitCode = 1;
  }
}

void main();
