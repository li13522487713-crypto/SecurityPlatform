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
  return (expectSuccess ? envelope?.data : envelope) as Json;
}

async function createResource(name: string): Promise<string> {
  const created = await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name,
      displayName: name,
      description: "Created by verify-microflow-callstack-runtime.ts",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "runtime-callstack"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  assert(created.id, "created resource id should exist");
  return String(created.id);
}

async function saveSchema(resourceId: string, schema: Json): Promise<void> {
  await api("PUT", `/api/microflows/${resourceId}/schema`, {
    saveReason: "round 56 callstack verify",
    schema,
  });
}

function baseSchema(resourceId: string, name: string, returnType: Json, parameters: Json[], objects: Json[], flows: Json[]): Json {
  return {
    schemaVersion: "1.0.0",
    id: resourceId,
    stableId: resourceId,
    name,
    moduleId: "verify-module",
    moduleName: "Verify",
    returnType,
    parameters,
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
  };
}

function childReturnStringSchema(resourceId: string, name: string): Json {
  return baseSchema(
    resourceId,
    name,
    { kind: "string" },
    [{ id: "param-name", name: "name", type: { kind: "string" }, required: true }],
    [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent", returnValue: "$name", returnType: { kind: "string" } },
    ],
    [{ id: "f-start-end", kind: "sequence", originObjectId: "start", destinationObjectId: "end" }],
  );
}

function parentCallSchema(resourceId: string, name: string, childId: string, childQualifiedName?: string): Json {
  return baseSchema(
    resourceId,
    name,
    { kind: "void" },
    [],
    [
      { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
      {
        id: "call-child",
        kind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        action: {
          id: "call-child-action",
          kind: "callMicroflow",
          officialType: "Microflows$MicroflowCallAction",
          targetMicroflowId: childId,
          targetMicroflowQualifiedName: childQualifiedName,
          parameterMappings: [{ id: "pm-name", parameterName: "name", argumentExpression: { raw: "\"hello child\"" } }],
          returnValue: { storeResult: true, outputVariableName: "childGreeting" },
          transactionBoundary: "sharedTransaction",
        },
      },
      { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
    ],
    [
      { id: "f-start-call", kind: "sequence", originObjectId: "start", destinationObjectId: "call-child" },
      { id: "f-call-end", kind: "sequence", originObjectId: "call-child", destinationObjectId: "end" },
    ],
  );
}

function missingParameterSchema(resourceId: string, childId: string): Json {
  const schema = parentCallSchema(resourceId, "Round56MissingParameter", childId);
  schema.objectCollection.objects[1].action.parameterMappings = [];
  return schema;
}

function selfRecursionSchema(resourceId: string): Json {
  const schema = parentCallSchema(resourceId, "Round56SelfRecursion", resourceId);
  schema.objectCollection.objects[1].action.parameterMappings = [];
  schema.objectCollection.objects[1].action.returnValue = { storeResult: false };
  return schema;
}

async function testRun(resourceId: string, schema: Json, expectSuccess = true): Promise<Json> {
  const data = await api("POST", `/api/microflows/${resourceId}/test-run`, { input: {}, options: { maxSteps: 80 }, schema }, expectSuccess);
  return (expectSuccess ? data?.session : data) as Json;
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");
  const suffix = Date.now();
  const childId = await createResource(`Round56Child${suffix}`);
  const parentId = await createResource(`Round56Parent${suffix}`);
  const childSchema = childReturnStringSchema(childId, `Round56Child${suffix}`);
  await saveSchema(childId, childSchema);

  const parent = await testRun(parentId, parentCallSchema(parentId, `Round56Parent${suffix}`, childId, `Verify.Round56Child${suffix}`));
  assert(parent.status === "success", "parent call should succeed");
  const callFrame = parent.trace.find((frame: Json) => frame.objectId === "call-child");
  assert(callFrame?.output?.callMicroflow, "parent trace should contain output.callMicroflow");
  assert(callFrame.output.callMicroflow.childRunId, "callMicroflow output should contain childRunId");
  assert(callFrame.output.callMicroflow.parameterBindings?.[0]?.valuePreview === "hello child", "parameter binding preview should be visible");
  assert(callFrame.output.callMicroflow.returnBinding?.outputVariableName === "childGreeting", "return binding should store parent variable");
  assert(callFrame.variablesSnapshot?.childGreeting?.valuePreview === "hello child", "parent VariableStore should receive child return");
  assert(parent.childRuns?.length === 1 || parent.childRunIds?.length === 1, "parent session should link child run");

  const childTrace = await api("GET", `/api/microflows/runs/${callFrame.output.callMicroflow.childRunId}/trace`);
  assert(childTrace.trace?.some((frame: Json) => frame.parentRunId === parent.id), "child trace should carry parentRunId");
  assert(childTrace.trace?.some((frame: Json) => frame.callDepth === 1), "child trace should carry callDepth");

  const missing = await testRun(parentId, missingParameterSchema(parentId, childId), false);
  assert(missing?.success === false || missing?.status === "failed" || missing?.data?.session?.status === "failed", "missing required parameter should fail");

  const recursion = await testRun(parentId, selfRecursionSchema(parentId), false);
  const recursionSession = recursion?.data?.session ?? recursion?.session ?? recursion;
  assert(recursionSession?.status === "failed", "direct recursion should fail");
  assert(JSON.stringify(recursionSession).includes("RUNTIME_CALL_RECURSION_DETECTED"), "recursion error code should be visible");
  assert(!JSON.stringify(parent).includes("FlowGram"), "call trace must not contain FlowGram JSON");

  console.log("verify-microflow-callstack-runtime: PASS");
}

run().catch((error) => {
  console.error("verify-microflow-callstack-runtime: FAIL");
  console.error(error);
  process.exitCode = 1;
});
