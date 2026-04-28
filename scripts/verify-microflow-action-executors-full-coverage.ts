import { readFileSync } from "node:fs";
import { join } from "node:path";

const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";
let resourceId = process.env.MICROFLOW_ID ?? "";
const root = process.cwd();

type Json = Record<string, unknown>;

const frontendActionKinds = [
  "retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "cast",
  "aggregateList", "createList", "changeList", "listOperation",
  "createVariable", "changeVariable",
  "callMicroflow", "callJavaAction", "callJavaScriptAction", "callNanoflow",
  "restCall", "webServiceCall", "importXml", "exportXml", "callExternalAction", "restOperationCall",
  "closePage", "downloadFile", "showHomePage", "showMessage", "showPage", "validationFeedback", "synchronize",
  "logMessage", "generateDocument",
  "counter", "incrementCounter", "gauge", "mlModelCall",
  "applyJumpToOption", "callWorkflow", "changeWorkflowState", "completeUserTask", "generateJumpToOptions",
  "retrieveWorkflowActivityRecords", "retrieveWorkflowContext", "retrieveWorkflows", "showUserTaskPage",
  "showWorkflowAdminPage", "lockWorkflow", "unlockWorkflow", "notifyWorkflow",
  "deleteExternalObject", "sendExternalObject",
];

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

async function api(method: string, path: string, body?: unknown): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (!response.ok || envelope?.success !== true) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return envelope.data as Json;
}

async function ensureResource(): Promise<void> {
  if (resourceId) {
    return;
  }

  const created = await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name: `RuntimeRound54${Date.now()}`,
      displayName: "Runtime Round 54 ActionExecutor Verify",
      description: "Created by verify-microflow-action-executors-full-coverage.ts",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "runtime-action-executor"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  resourceId = String(created.id ?? "");
  assert(resourceId, "created resource id should be returned");
}

function verifySourceCoverage(): void {
  const registry = read("src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs");
  const models = read("src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionRuntimeModels.cs");
  const runner = read("src/backend/Atlas.Application.Microflows/Services/MicroflowMockRuntimeRunner.cs");
  const supportMatrix = read("src/backend/Atlas.Application.Microflows/Services/MicroflowExecutionPlanServices.cs");
  const validation = read("src/backend/Atlas.Application.Microflows/Services/MicroflowValidationService.cs");

  for (const kind of frontendActionKinds) {
    assert(registry.includes(`"${kind}"`), `missing executor descriptor for ${kind}`);
  }

  for (const required of [
    "IMicroflowActionExecutor",
    "IMicroflowActionExecutorRegistry",
    "MicroflowActionExecutionContext",
    "MicroflowActionExecutionResult",
    "MicroflowRuntimeCommand",
    "IMicroflowRuntimeConnectorRegistry",
    "MicroflowConnectorExecutionRequest",
    "MicroflowConnectorExecutionResult",
  ]) {
    assert(models.includes(required), `${required} is missing`);
  }

  assert(runner.includes("ActionExecutorRegistry") && runner.includes("WithActionExecutionPreview"), "TestRun must use ActionExecutorRegistry output");
  assert(supportMatrix.includes("MicroflowActionExecutorRegistry.BuiltInDescriptors"), "SupportMatrix must align to ActionExecutorRegistry");
  assert(validation.includes("_actionSupportMatrix.Resolve"), "ValidationService must align to ActionSupportMatrix");
  assert(!/SaveChanges|BeginTran|CommitTran|RollbackTran|SqlSugar|DbContext|ExecuteCommand/.test(registry + runner), "ActionExecutor runtime must not write business DB");
  assert(!/FlowGram|workflowJson|nodes\\[\\]|edges\\[\\]/.test(registry + runner), "ActionExecutor output must not leak FlowGram JSON");
}

function schema(name: string, objects: Json[], flows: Json[]): Json {
  return {
    schemaVersion: "1.0.0",
    id: `verify-${name}`,
    name,
    moduleId: "verify-module",
    parameters: [],
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

async function testRun(testSchema: Json, options: Json = {}): Promise<Json> {
  const result = await api("POST", `/api/microflows/${resourceId}/test-run`, { schema: testSchema, input: {}, options });
  return result.session as Json;
}

function trace(session: Json): Json[] {
  return session.trace as Json[];
}

async function verifyRuntimeSmoke(): Promise<void> {
  await api("GET", "/api/microflows/health");
  await ensureResource();

  const objectAndListObjects: Json[] = [
    { id: "start", kind: "startEvent" },
    { id: "create", kind: "actionActivity", action: { id: "create-action", kind: "createObject", entityQualifiedName: "Sales.Order", outputVariableName: "order", commit: { enabled: true } } },
    { id: "cast", kind: "actionActivity", action: { id: "cast-action", kind: "cast", inputVariableName: "order", outputVariableName: "specializedOrder", targetEntityQualifiedName: "Sales.SpecialOrder" } },
    { id: "createList", kind: "actionActivity", action: { id: "create-list-action", kind: "createList", outputVariableName: "orders", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } } },
    { id: "changeList", kind: "actionActivity", action: { id: "change-list-action", kind: "changeList", listVariableName: "orders", operation: "add", valueExpression: "$order" } },
    { id: "aggregate", kind: "actionActivity", action: { id: "aggregate-action", kind: "aggregateList", sourceListVariableName: "orders", outputVariableName: "orderCount", aggregate: "count" } },
    { id: "message", kind: "actionActivity", action: { id: "message-action", kind: "showMessage", template: { text: "Saved" }, level: "info" } },
    { id: "end", kind: "endEvent" },
  ];
  const objectAndList = await testRun(schema("ActionExecutorObjectListUi", objectAndListObjects, chain(objectAndListObjects)));
  assert(objectAndList.status === "success", "object/list/ui smoke should succeed");
  assert(JSON.stringify(objectAndList).includes("\"executorCategory\""), "trace output should include executorCategory");
  assert(JSON.stringify(objectAndList).includes("\"runtimeCommands\""), "showMessage should produce runtimeCommands");
  assert(JSON.stringify(objectAndList).includes("\"transaction\""), "object actions should include transaction preview");

  const connectorObjects: Json[] = [
    { id: "start", kind: "startEvent" },
    { id: "soap", kind: "actionActivity", action: { id: "soap-action", kind: "webServiceCall" } },
    { id: "end", kind: "endEvent" },
  ];
  try {
    const connector = await testRun(schema("ActionExecutorConnectorRequired", connectorObjects, chain(connectorObjects)));
    assert(connector.status === "failed", "connector-backed action without capability should fail");
    assert(JSON.stringify(trace(connector)).includes("RUNTIME_CONNECTOR_REQUIRED"), "connector failure should expose RUNTIME_CONNECTOR_REQUIRED");
  } catch (error) {
    const message = String(error);
    assert(message.includes("MICROFLOW_VALIDATION_FAILED") && message.includes("web service connector"), "connector missing should be blocked before fake success");
  }

  const unsupportedObjects: Json[] = [
    { id: "start", kind: "startEvent" },
    { id: "nanoflow", kind: "actionActivity", action: { id: "nanoflow-action", kind: "callNanoflow" } },
    { id: "end", kind: "endEvent" },
  ];
  try {
    const unsupported = await testRun(schema("ActionExecutorUnsupported", unsupportedObjects, chain(unsupportedObjects)));
    assert(unsupported.status === "failed", "nanoflow-only action should fail in server runtime");
    assert(JSON.stringify(trace(unsupported)).includes("RUNTIME_UNSUPPORTED_ACTION"), "unsupported action should expose RUNTIME_UNSUPPORTED_ACTION");
  } catch (error) {
    const message = String(error);
    assert(message.includes("MICROFLOW_VALIDATION_FAILED") && message.includes("Nanoflow"), "nanoflow-only action should be blocked before fake success");
  }
}

async function run(): Promise<void> {
  verifySourceCoverage();
  await verifyRuntimeSmoke();
  console.log("verify-microflow-action-executors-full-coverage: PASS");
}

run().catch(error => {
  console.error("verify-microflow-action-executors-full-coverage: FAIL");
  console.error(error);
  process.exitCode = 1;
});
