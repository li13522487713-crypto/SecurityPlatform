import { readFileSync } from "node:fs";
import { join } from "node:path";

const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";
const resourceId = process.env.MICROFLOW_ID ?? "mf-seed-blank";
const root = process.cwd();

type Json = Record<string, unknown>;

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

function makeId(prefix: string): string {
  return `${prefix}${Date.now()}${Math.random().toString(36).slice(2, 8)}`;
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

function verifySourceContracts(): void {
  const manager = read("src/backend/Atlas.Application.Microflows/Runtime/Transactions/MicroflowTransactionManager.cs");
  const managerInterface = read("src/backend/Atlas.Application.Microflows/Runtime/Transactions/IMicroflowTransactionManager.cs");
  const unitOfWork = read("src/backend/Atlas.Application.Microflows/Runtime/Transactions/MicroflowUnitOfWork.cs");
  const models = read("src/backend/Atlas.Application.Microflows/Runtime/Transactions/MicroflowRuntimeTransactionModels.cs");
  const context = read("src/backend/Atlas.Application.Microflows/Runtime/RuntimeExecutionContext.cs");
  const runner = read("src/backend/Atlas.Application.Microflows/Services/MicroflowMockRuntimeRunner.cs");

  for (const [name, content] of Object.entries({ manager, unitOfWork, models, context, runner })) {
    assert(!/SaveChanges|BeginTran|CommitTran|RollbackTran|SqlSugar|DbContext|ExecuteCommand/.test(content), `${name} must not write business DB`);
    assert(!/FlowGram|workflowJson|nodes\[\]|edges\[\]/.test(content), `${name} must not store FlowGram JSON in transaction snapshots`);
  }

  for (const method of ["Begin", "Commit", "Rollback", "CreateSavepoint", "RollbackToSavepoint", "TrackCreate", "TrackUpdate", "TrackDelete", "TrackRollbackObject", "TrackCommitAction", "CreateSnapshot"]) {
    assert(managerInterface.includes(method), `IMicroflowTransactionManager.${method} is missing`);
  }
  assert(unitOfWork.includes("MarkCommitted") && unitOfWork.includes("MarkRolledBack"), "UnitOfWork commit/rollback semantics are missing");
  assert(models.includes("MicroflowRuntimeTransactionContext"), "TransactionContext model is missing");
  assert(context.includes("TransactionDiagnostics"), "RuntimeExecutionContext transaction diagnostics are missing");
  assert(runner.includes("WithTransactionPreview"), "MockRuntimeRunner transaction preview is missing");
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
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
  };
}

function chain(objects: Json[]): Json[] {
  const flows: Json[] = [];
  for (let index = 0; index < objects.length - 1; index++) {
    flows.push({
      id: `f-${String(objects[index].id)}-${String(objects[index + 1].id)}`,
      kind: "sequence",
      originObjectId: objects[index].id,
      destinationObjectId: objects[index + 1].id,
    });
  }
  return flows;
}

function objectActionSchema(): Json {
  const objects: Json[] = [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    {
      id: "create",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      action: {
        id: "create-action",
        kind: "createObject",
        entityQualifiedName: "Sales.Order",
        outputVariableName: "order",
        memberChanges: [{ memberQualifiedName: "Sales.Order.Number", valueExpression: "'ORD-1'" }],
        commit: { enabled: true },
        withEvents: true,
        refreshInClient: true,
      },
    },
    {
      id: "change",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      action: {
        id: "change-action",
        kind: "changeMembers",
        changeVariableName: "order",
        memberChanges: [{ memberQualifiedName: "Sales.Order.Status", valueExpression: "'Paid'" }],
        commit: { enabled: true },
        validateObject: true,
      },
    },
    { id: "commit", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "commit-action", kind: "commit", objectOrListVariableName: "order", withEvents: true, refreshInClient: true } },
    { id: "delete", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "delete-action", kind: "delete", objectOrListVariableName: "order", withEvents: true, deleteBehavior: "deleteOnly" } },
    { id: "rollbackObject", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "rollback-action", kind: "rollback", objectOrListVariableName: "order", refreshInClient: true } },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ];
  return baseSchema("TransactionObjectActions", objects, chain(objects));
}

function restErrorSchema(errorHandlingType: "rollback" | "customWithRollback" | "customWithoutRollback" | "continue"): Json {
  const objects: Json[] = [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "create", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "create-action", kind: "createObject", entityQualifiedName: "Sales.Order", outputVariableName: "order" } },
    { id: "rest", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: "rest-action", kind: "restCall", errorHandlingType } },
    { id: "normalEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
    { id: "handledEnd", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ];
  const flows: Json[] = [
    { id: "f-start-create", kind: "sequence", originObjectId: "start", destinationObjectId: "create" },
    { id: "f-create-rest", kind: "sequence", originObjectId: "create", destinationObjectId: "rest" },
    { id: "f-rest-normal", kind: "sequence", originObjectId: "rest", destinationObjectId: "normalEnd" },
  ];
  if (errorHandlingType === "customWithRollback" || errorHandlingType === "customWithoutRollback") {
    flows.push({ id: "f-rest-error", kind: "sequence", originObjectId: "rest", destinationObjectId: "handledEnd", isErrorHandler: true, editor: { edgeKind: "errorHandler" } });
  }
  return baseSchema(`TransactionRestError${errorHandlingType}`, objects, flows);
}

async function testRun(schema: Json, options: Json = {}): Promise<Json> {
  const result = await api("POST", `/api/microflows/${resourceId}/test-run`, { schema, input: {}, options });
  return result.session as Json;
}

function frames(session: Json): Json[] {
  return session.trace as Json[];
}

function logs(session: Json): Json[] {
  return session.logs as Json[];
}

function transaction(frame: Json): Json {
  return ((frame.output as Json)?.transaction ?? {}) as Json;
}

async function verifyRuntimeApi(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const objectSession = await testRun(objectActionSchema());
  assert((objectSession.transactionSummary as Json).status === "committed", "successful run should commit transaction");
  assert(Number((objectSession.transactionSummary as Json).changedObjectCount) >= 5, "object actions should add changed objects");
  assert(Number((objectSession.transactionSummary as Json).committedObjectCount) >= 2, "commit action should add committed objects");
  assert(Number((objectSession.transactionSummary as Json).rolledBackObjectCount) >= 1, "rollback action should add rolled back object operation");
  assert(JSON.stringify(objectSession.output ?? {}).includes("transactionSummary"), "RunSession output should include transactionSummary");

  const createFrame = frames(objectSession).find(frame => frame.objectId === "create")!;
  const changeFrame = frames(objectSession).find(frame => frame.objectId === "change")!;
  const commitFrame = frames(objectSession).find(frame => frame.objectId === "commit")!;
  const deleteFrame = frames(objectSession).find(frame => frame.objectId === "delete")!;
  const rollbackFrame = frames(objectSession).find(frame => frame.objectId === "rollbackObject")!;
  assert(transaction(createFrame).operation === "createObject", "CreateObject trace should include transaction create preview");
  assert(transaction(changeFrame).operation === "changeMembers", "ChangeMembers trace should include transaction update preview");
  assert(transaction(commitFrame).operation === "commit", "CommitAction trace should include transaction preview");
  assert(transaction(deleteFrame).operation === "delete", "DeleteAction trace should include transaction delete preview");
  assert(transaction(rollbackFrame).operation === "rollback", "RollbackAction trace should include rollback object preview");
  assert(logs(objectSession).some(log => String(log.message).includes("transaction.begin")), "transaction begin log should be present");
  assert(logs(objectSession).some(log => String(log.message).includes("transaction.savepoint")), "transaction savepoint log should be present");
  assert(logs(objectSession).some(log => String(log.message).includes("transaction.stageCreate")), "transaction create log should be present");
  assert(logs(objectSession).some(log => String(log.message).includes("transaction.stageUpdate")), "transaction update log should be present");
  assert(logs(objectSession).some(log => String(log.message).includes("transaction.stageDelete")), "transaction delete log should be present");

  const trace = await api("GET", `/api/microflows/runs/${String(objectSession.id)}/trace`);
  assert(JSON.stringify(trace.trace).includes("\"transaction\""), "GET run trace should include transaction output");
  const reloaded = await api("GET", `/api/microflows/runs/${String(objectSession.id)}`);
  assert(((reloaded.transactionSummary as Json)?.status ?? (reloaded as Json).session) !== undefined, "GET run session should be queryable");

  const rollbackSession = await testRun(restErrorSchema("rollback"), { simulateRestError: true });
  assert(rollbackSession.status === "failed", "rollback error handling should fail without handler path");
  assert((rollbackSession.transactionSummary as Json).status === "rolledBack", "rollback error handling should rollback transaction");

  const customWith = await testRun(restErrorSchema("customWithRollback"), { simulateRestError: true });
  assert(customWith.status === "success", "customWithRollback handler path should complete");
  assert((customWith.transactionSummary as Json).status === "rolledBack", "customWithRollback should rollback transaction");

  const customWithout = await testRun(restErrorSchema("customWithoutRollback"), { simulateRestError: true });
  assert(customWithout.status === "success", "customWithoutRollback handler path should complete");
  assert((customWithout.transactionSummary as Json).status === "committed", "customWithoutRollback should not rollback transaction");

  const continued = await testRun(restErrorSchema("continue"), { simulateRestError: true });
  assert(continued.status === "success", "continue error handling should continue normal path");
  assert((continued.transactionSummary as Json).status === "committed", "continue should not rollback transaction");

  const serialized = JSON.stringify(objectSession);
  assert(!serialized.includes("\"nodes\"") && !serialized.includes("\"edges\"") && !serialized.includes("workflowJson"), "transaction snapshot must not leak FlowGram JSON");
}

async function run(): Promise<void> {
  verifySourceContracts();
  await verifyRuntimeApi();
  console.log("verify-microflow-transaction-manager: PASS");
}

run().catch(error => {
  console.error("verify-microflow-transaction-manager: FAIL");
  console.error(error);
  process.exitCode = 1;
});
