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

function schema(id: string, name: string): Json {
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    name,
    displayName: name,
    moduleId: "verify-module",
    parameters: [
      { id: "p-user", name: "userId", type: { kind: "string" }, required: true, documentation: "User id." },
    ],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root-collection",
      objects: [
        { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
        {
          id: "retrieve",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          caption: "Retrieve orders",
          action: {
            id: "retrieve-action",
            kind: "retrieve",
            officialType: "Microflows$RetrieveAction",
            retrieveSource: { kind: "database", entityQualifiedName: "Sales.Order" },
            outputVariableName: "orders",
          },
        },
        {
          id: "decision",
          kind: "exclusiveSplit",
          officialType: "Microflows$ExclusiveSplit",
          caption: "Has orders?",
        },
        {
          id: "loop",
          kind: "loopedActivity",
          officialType: "Microflows$LoopedActivity",
          objectCollection: {
            id: "loop-collection",
            objects: [
              { id: "loop-start", kind: "startEvent", officialType: "Microflows$StartEvent" },
              {
                id: "log",
                kind: "actionActivity",
                officialType: "Microflows$ActionActivity",
                action: { id: "log-action", kind: "logMessage", officialType: "Microflows$LogMessageAction", level: "info", template: { text: "loop" } },
              },
              { id: "loop-end", kind: "endEvent", officialType: "Microflows$EndEvent" },
            ],
            flows: [
              { id: "lf-start-log", kind: "sequence", originObjectId: "loop-start", destinationObjectId: "log" },
              { id: "lf-log-end", kind: "sequence", originObjectId: "log", destinationObjectId: "loop-end" },
            ],
          },
        },
        {
          id: "rest",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          action: {
            id: "rest-action",
            kind: "restCall",
            officialType: "Microflows$RestCallAction",
            importMappingQualifiedName: "Api.OrderResponse",
            outputVariableName: "restResponse",
            errorHandlingType: "customWithoutRollback",
          },
        },
        { id: "error", kind: "errorEvent", officialType: "Microflows$ErrorEvent" },
        { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
        { id: "note", kind: "annotation", officialType: "Microflows$Annotation", caption: "ignored" },
      ],
      flows: [
        { id: "f-start-retrieve", kind: "sequence", originObjectId: "start", destinationObjectId: "retrieve" },
        { id: "f-retrieve-decision", kind: "sequence", originObjectId: "retrieve", destinationObjectId: "decision" },
        { id: "f-decision-loop", kind: "sequence", originObjectId: "decision", destinationObjectId: "loop", editor: { edgeKind: "decisionCondition", branchOrder: 0 }, caseValues: [{ kind: "boolean", value: true }] },
        { id: "f-decision-end", kind: "sequence", originObjectId: "decision", destinationObjectId: "end", editor: { edgeKind: "decisionCondition", branchOrder: 1 }, caseValues: [{ kind: "boolean", value: false }] },
        { id: "f-loop-rest", kind: "sequence", originObjectId: "loop", destinationObjectId: "rest" },
        { id: "f-rest-end", kind: "sequence", originObjectId: "rest", destinationObjectId: "end" },
        { id: "f-rest-error", kind: "sequence", originObjectId: "rest", destinationObjectId: "error", isErrorHandler: true, editor: { edgeKind: "errorHandler" } },
        { id: "f-note", kind: "annotation", originObjectId: "note", destinationObjectId: "decision", edgeKind: "annotation" },
      ],
    },
    flows: [],
  };
}

function modeledOnlySchema(id: string): Json {
  const value = schema(id, "ModeledOnlyPlan");
  const root = value.objectCollection as Json;
  const objects = root.objects as Json[];
  objects.splice(2, 0, {
    id: "show-page",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    action: { id: "show-page-action", kind: "showPage", officialType: "Microflows$ShowPageAction" },
  });
  const flows = root.flows as Json[];
  flows[1] = { id: "f-retrieve-show-page", kind: "sequence", originObjectId: "retrieve", destinationObjectId: "show-page" };
  flows.push({ id: "f-show-page-decision", kind: "sequence", originObjectId: "show-page", destinationObjectId: "decision" });
  return value;
}

function invalidFlowSchema(id: string): Json {
  const value = schema(id, "InvalidPlan");
  const root = value.objectCollection as Json;
  (root.flows as Json[]).push({ id: "f-invalid", kind: "sequence", originObjectId: "missing", destinationObjectId: "end" });
  return value;
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function assertNoFlowGramJson(plan: Json): void {
  const text = JSON.stringify(plan);
  assert(!text.includes("\"nodes\"") || text.includes("\"startNodeId\""), "plan should be execution-plan shaped, not FlowGram JSON");
  assert(!text.includes("\"edges\""), "plan must not contain FlowGram edges JSON");
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const id = makeId("VerifyExecutionPlan");
  const inlineSchema = schema(id, id);
  const inlinePlan = (await api("POST", "/api/microflows/runtime/plan", {
    schema: inlineSchema,
    options: { mode: "validateOnly", failOnUnsupported: false },
  })).data as Json;

  assert(inlinePlan.startNodeId === "start", "inline plan should contain startNodeId");
  assert(Array.isArray(inlinePlan.endNodeIds) && (inlinePlan.endNodeIds as unknown[]).includes("end"), "inline plan should contain endNodeIds");
  assert((inlinePlan.nodes as Json[]).some(node => node.actionKind === "retrieve" && node.supportLevel === "supported"), "P0 retrieve should be supported");
  assert((inlinePlan.nodes as Json[]).some(node => node.actionKind === "restCall" && node.supportLevel === "supported"), "P0 restCall should be supported");
  assert((inlinePlan.normalFlows as Json[]).some(flow => flow.flowId === "f-start-retrieve"), "plan should contain normalFlows");
  assert((inlinePlan.decisionFlows as Json[]).length >= 2, "plan should contain decisionFlows");
  assert((inlinePlan.errorHandlerFlows as Json[]).some(flow => flow.isErrorHandler === true), "plan should contain errorHandlerFlows");
  assert((inlinePlan.ignoredFlows as Json[]).some(flow => flow.flowId === "f-note"), "annotation flow should be ignored");
  assert((inlinePlan.loopCollections as Json[]).some(loop => loop.loopObjectId === "loop"), "plan should contain loopCollections");
  assert((inlinePlan.variableDeclarations as Json[]).some(variable => variable.name === "userId"), "parameter variable should be declared");
  assert((inlinePlan.variableDeclarations as Json[]).some(variable => variable.name === "orders"), "retrieve output variable should be declared");
  assert((inlinePlan.metadataRefs as Json[]).some(ref => ref.kind === "entity" && ref.qualifiedName === "Sales.Order"), "metadataRefs should include entity");
  assertNoFlowGramJson(inlinePlan);

  const modeledPlan = (await api("POST", "/api/microflows/runtime/plan", {
    schema: modeledOnlySchema(makeId("VerifyModeledOnly")),
    options: { mode: "validateOnly", failOnUnsupported: false },
  })).data as Json;
  assert((modeledPlan.unsupportedActions as Json[]).some(item => item.supportLevel === "modeledOnly"), "modeledOnly action should appear in unsupportedActions");

  await api("POST", "/api/microflows/runtime/plan", {
    schema: modeledOnlySchema(makeId("VerifyFailUnsupported")),
    options: { mode: "validateOnly", failOnUnsupported: true },
  }, false);

  const invalidPlan = (await api("POST", "/api/microflows/runtime/plan", {
    schema: invalidFlowSchema(makeId("VerifyInvalidFlow")),
    options: { mode: "validateOnly", failOnUnsupported: false },
  })).data as Json;
  assert((invalidPlan.diagnostics as Json[]).some(item => item.code === "RUNTIME_FLOW_ORIGIN_NOT_FOUND"), "invalid flow should produce diagnostic");

  const name = makeId("VerifyPlanResource");
  const resource = (await api("POST", "/api/microflows", {
    workspaceId,
    input: { name, displayName: name, moduleId: "verify-module", moduleName: "Verify", tags: ["verify", "round48"] },
  })).data as Json;
  await api("PUT", `/api/microflows/${resource.id}/schema`, { saveReason: "verify runtime plan", schema: inlineSchema });
  const currentPlan = (await api("GET", `/api/microflows/${resource.id}/runtime/plan?mode=validateOnly`)).data as Json;
  assert(currentPlan.resourceId === resource.id, "current resource plan should include resourceId");
  const published = (await api("POST", `/api/microflows/${resource.id}/publish`, { version: "1.0.0", description: "verify runtime plan", confirmBreakingChanges: false })).data as Json;
  const version = published.version as Json;
  const versionPlan = (await api("GET", `/api/microflows/${resource.id}/versions/${version.id}/runtime/plan?mode=publishedRun`)).data as Json;
  assert(versionPlan.version === "1.0.0", "version plan should include published version");

  console.log("PASS verify-microflow-execution-plan-loader");
}

run().catch(error => {
  console.error("FAIL verify-microflow-execution-plan-loader");
  console.error(error);
  process.exit(1);
});
