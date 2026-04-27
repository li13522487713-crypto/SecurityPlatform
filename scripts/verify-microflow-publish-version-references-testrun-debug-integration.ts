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
  if (!expectSuccess && envelope.success !== false) {
    throw new Error(`${method} ${path} expected error envelope`);
  }
  return envelope;
}

function makeId(prefix: string): string {
  return `${prefix}${Date.now()}${Math.random().toString(36).slice(2, 8)}`;
}

function schema(id: string, name: string, objects: Json[], flows: Json[], parameters: Json[] = [], returnType: Json = { kind: "void" }): Json {
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    name,
    displayName: name,
    moduleId: "verify-module",
    moduleName: "Verify",
    parameters,
    returnType,
    objectCollection: { id: "root-collection", objects },
    flows,
    variables: {},
    validation: { issues: [] },
  };
}

async function createResource(name: string, parameters: Json[] = []): Promise<Json> {
  return (await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name,
      displayName: name,
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "round46-47"],
      parameters,
      returnType: { kind: "void" },
    },
  })).data;
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const mainName = makeId("VerifyPublishRuntime");
  const main = await createResource(mainName, [{ id: "p-name", name: "name", dataType: "String", required: true }]);
  const mainSchema = schema(main.id, mainName, [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "log", kind: "actionActivity", action: { id: "log-action", kind: "logMessage", template: { text: "verify run" }, level: "info" } },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ], [
    { id: "f-start-log", kind: "sequence", originObjectId: "start", destinationObjectId: "log" },
    { id: "f-log-end", kind: "sequence", originObjectId: "log", destinationObjectId: "end" },
  ], [{ id: "p-name", name: "name", dataType: "String", required: true }]);
  await api("PUT", `/api/microflows/${main.id}/schema`, { saveReason: "verify baseline", schema: mainSchema });
  const validation = (await api("POST", `/api/microflows/${main.id}/validate`, { mode: "testRun", includeWarnings: true, schema: mainSchema })).data;
  if (validation.summary.errorCount !== 0) {
    throw new Error("testRun validation should pass");
  }

  const publish = (await api("POST", `/api/microflows/${main.id}/publish`, { version: "1.0.0", description: "verify publish", confirmBreakingChanges: false })).data;
  const versionId = publish.version.id;
  await api("GET", `/api/microflows/${main.id}/versions`);
  await api("GET", `/api/microflows/${main.id}/versions/${versionId}`);
  await api("GET", `/api/microflows/${main.id}/versions/${versionId}/compare-current`);
  await api("GET", `/api/microflows/${main.id}/impact?version=1.0.1&includeBreakingChanges=true&includeReferences=true`);

  const duplicate = (await api("POST", `/api/microflows/${main.id}/versions/${versionId}/duplicate`, {
    name: makeId("VerifyDuplicate"),
    displayName: "Verify Duplicate",
    moduleId: "verify-module",
    tags: ["verify"],
  })).data;
  if (duplicate.publishStatus !== "neverPublished") {
    throw new Error("duplicate version should create neverPublished draft resource");
  }
  await api("POST", `/api/microflows/${main.id}/versions/${versionId}/rollback`, { reason: "verify rollback" });

  const target = await createResource(makeId("VerifyTarget"), [{ id: "p-user", name: "userId", dataType: "String", required: true }]);
  const source = await createResource(makeId("VerifySource"));
  const sourceSchema = schema(source.id, source.name, [
    { id: "start", kind: "startEvent" },
    { id: "call", kind: "actionActivity", action: { id: "call-action", kind: "callMicroflow", targetMicroflowId: target.id } },
    { id: "end", kind: "endEvent" },
  ], [
    { id: "f-start-call", originObjectId: "start", destinationObjectId: "call" },
    { id: "f-call-end", originObjectId: "call", destinationObjectId: "end" },
  ]);
  await api("PUT", `/api/microflows/${source.id}/schema`, { saveReason: "verify references", schema: sourceSchema });
  await api("POST", `/api/microflows/${source.id}/references/rebuild`);
  const refs = (await api("GET", `/api/microflows/${target.id}/references?includeInactive=false&sourceType=microflow&impactLevel=medium`)).data;
  if (!Array.isArray(refs) || refs.length === 0) {
    throw new Error("target references should include source callMicroflow");
  }
  await api("POST", `/api/microflows/${target.id}/publish`, { version: "1.0.0", description: "target baseline", confirmBreakingChanges: false });
  await api("PUT", `/api/microflows/${target.id}/schema`, { saveReason: "verify breaking", schema: schema(target.id, target.name, [{ id: "start", kind: "startEvent" }, { id: "end", kind: "endEvent" }], [{ id: "f", originObjectId: "start", destinationObjectId: "end" }]) });
  const impact = (await api("GET", `/api/microflows/${target.id}/impact?version=1.1.0&includeBreakingChanges=true&includeReferences=true`)).data;
  if (impact.summary.highImpactCount === 0) {
    throw new Error("breaking parameter removal should be high impact");
  }
  await api("POST", `/api/microflows/${target.id}/publish`, { version: "1.1.0", confirmBreakingChanges: false }, false);
  await api("POST", `/api/microflows/${target.id}/publish`, { version: "1.1.0", confirmBreakingChanges: true });

  const testRun = (await api("POST", `/api/microflows/${main.id}/test-run`, {
    input: { name: "demo" },
    options: { decisionBooleanResult: true, loopIterations: 2, simulateRestError: false, maxSteps: 50 },
    schema: mainSchema,
  })).data.session;
  if (!testRun.id || !Array.isArray(testRun.trace) || testRun.trace.length === 0) {
    throw new Error("test-run session shape invalid");
  }
  const runSession = (await api("GET", `/api/microflows/runs/${testRun.id}`)).data;
  const trace = (await api("GET", `/api/microflows/runs/${testRun.id}/trace`)).data;
  if (!runSession.resourceId || !Array.isArray(trace.trace) || !Array.isArray(trace.logs)) {
    throw new Error("run session or trace query shape invalid");
  }
  await api("POST", `/api/microflows/runs/${testRun.id}/cancel`);
  await api("GET", "/api/microflows/runs/not-found", undefined, false);
}

try {
  await run();
  console.log("PASS microflow publish/version/references + testrun/debug integration");
} catch (error) {
  console.error(`FAIL microflow integration: ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 1;
}
