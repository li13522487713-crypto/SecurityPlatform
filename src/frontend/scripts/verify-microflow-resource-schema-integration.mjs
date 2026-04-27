const baseUrl = (process.env.MICROFLOW_API_BASE_URL ?? process.env.VITE_MICROFLOW_API_BASE_URL ?? "http://localhost:5002").replace(/\/+$/u, "");
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "verify-user";
const runId = Date.now().toString(36);
const createdName = `VerifyResource${runId}`;
const duplicateName = `${createdName}Copy`;

const state = {
  createdId: undefined,
  duplicateId: undefined,
  currentSchemaId: undefined,
};

function apiUrl(path) {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const resolvedPath = baseUrl.endsWith("/api") && normalizedPath.startsWith("/api/")
    ? normalizedPath.slice("/api".length)
    : normalizedPath;
  return `${baseUrl}${resolvedPath}`;
}

function headers(hasBody = false) {
  return {
    Accept: "application/json",
    "X-Workspace-Id": workspaceId,
    "X-Tenant-Id": tenantId,
    "X-User-Id": userId,
    ...(hasBody ? { "Content-Type": "application/json" } : {}),
  };
}

async function request(method, path, body) {
  const response = await fetch(apiUrl(path), {
    method,
    headers: headers(body !== undefined),
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : undefined;
  return { response, payload };
}

function assertEnvelope(payload, label) {
  if (!payload || typeof payload.success !== "boolean") {
    throw new Error(`${label}: response is not MicroflowApiResponse envelope.`);
  }
}

async function expectOk(label, method, path, body) {
  const { response, payload } = await request(method, path, body);
  assertEnvelope(payload, label);
  if (!response.ok || payload.success !== true) {
    throw new Error(`${label}: expected success, got HTTP ${response.status} ${payload?.error?.code ?? ""} ${payload?.error?.message ?? ""}`);
  }
  console.log(`PASS ${label}`);
  return payload.data;
}

async function expectError(label, method, path, body, status, code) {
  const { response, payload } = await request(method, path, body);
  assertEnvelope(payload, label);
  if (response.status !== status || payload.success !== false || payload.error?.code !== code) {
    throw new Error(`${label}: expected ${status}/${code}, got HTTP ${response.status} ${payload?.error?.code ?? ""}`);
  }
  console.log(`PASS ${label}`);
  return payload.error;
}

function makeSchema(resource, caption = "Start") {
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: resource.id,
    stableId: resource.id,
    name: resource.name,
    displayName: resource.displayName,
    description: resource.description,
    moduleId: resource.moduleId,
    moduleName: resource.moduleName,
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root-collection",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        {
          id: "start",
          stableId: "start",
          kind: "startEvent",
          officialType: "Microflows$StartEvent",
          caption,
          documentation: "",
          relativeMiddlePoint: { x: 320, y: 200 },
        },
        {
          id: "end",
          stableId: "end",
          kind: "endEvent",
          officialType: "Microflows$EndEvent",
          caption: "End",
          documentation: "",
          relativeMiddlePoint: { x: 560, y: 200 },
        },
      ],
    },
    flows: [
      {
        id: "flow-start-end",
        stableId: "flow-start-end",
        kind: "sequence",
        officialType: "Microflows$SequenceFlow",
        originObjectId: "start",
        destinationObjectId: "end",
        caseValues: [],
        isErrorHandler: false,
      },
    ],
    variables: { all: [] },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 } },
  };
}

async function cleanup() {
  for (const id of [state.duplicateId, state.createdId].filter(Boolean)) {
    try {
      await request("DELETE", `/api/microflows/${id}`);
    } catch {
      // Best-effort cleanup only.
    }
  }
}

async function main() {
  let resource = await expectOk("health", "GET", "/api/microflows/health");
  if (resource.status !== "ok") {
    throw new Error("health: unexpected status.");
  }

  await expectOk("list resources", "GET", "/api/microflows?pageIndex=1&pageSize=20&sortBy=updatedAt&sortOrder=desc");

  resource = await expectOk("create resource", "POST", "/api/microflows", {
    workspaceId,
    input: {
      name: createdName,
      displayName: createdName,
      description: "Created by verify-microflow-resource-schema-integration.",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "resource-schema"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  state.createdId = resource.id;
  state.currentSchemaId = resource.schemaId;

  await expectError("create duplicated name", "POST", "/api/microflows", {
    workspaceId,
    input: { name: createdName, moduleId: "verify-module", tags: [], parameters: [], returnType: { kind: "void" } },
  }, 409, "MICROFLOW_NAME_DUPLICATED");

  resource = await expectOk("get created resource", "GET", `/api/microflows/${state.createdId}`);
  const loadedSchema = await expectOk("get schema", "GET", `/api/microflows/${state.createdId}/schema`);
  if (!loadedSchema.schema?.objectCollection) {
    throw new Error("get schema: schema is not MicroflowAuthoringSchema.");
  }

  const saved = await expectOk("save schema", "PUT", `/api/microflows/${state.createdId}/schema`, {
    baseVersion: state.currentSchemaId,
    saveReason: "verify-save",
    schema: makeSchema(resource, "Verified Start"),
  });
  resource = saved.resource;
  state.currentSchemaId = resource.schemaId;

  const savedSchema = await expectOk("get schema after save", "GET", `/api/microflows/${state.createdId}/schema`);
  const startNode = savedSchema.schema.objectCollection.objects.find(item => item.id === "start");
  if (startNode?.caption !== "Verified Start") {
    throw new Error("get schema after save: saved caption was not persisted.");
  }

  await expectError("schema invalid save", "PUT", `/api/microflows/${state.createdId}/schema`, {
    baseVersion: state.currentSchemaId,
    schema: { nodes: [], edges: [] },
  }, 400, "MICROFLOW_SCHEMA_INVALID");

  await expectError("version conflict save", "PUT", `/api/microflows/${state.createdId}/schema`, {
    baseVersion: "stale-version",
    schema: makeSchema(resource, "Conflict Start"),
  }, 409, "MICROFLOW_VERSION_CONFLICT");

  resource = await expectOk("rename", "POST", `/api/microflows/${state.createdId}/rename`, {
    name: `${createdName}Renamed`,
    displayName: `${createdName} Renamed`,
  });

  resource = await expectOk("favorite", "POST", `/api/microflows/${state.createdId}/favorite`, { favorite: true });
  if (!resource.favorite) {
    throw new Error("favorite: resource.favorite was not persisted.");
  }

  const duplicate = await expectOk("duplicate", "POST", `/api/microflows/${state.createdId}/duplicate`, {
    name: duplicateName,
    displayName: `${createdName} Copy`,
    moduleId: "verify-module",
    tags: ["verify", "copy"],
  });
  state.duplicateId = duplicate.id;
  await expectOk("get duplicated schema", "GET", `/api/microflows/${state.duplicateId}/schema`);

  resource = await expectOk("archive", "POST", `/api/microflows/${state.createdId}/archive`);
  if (!resource.archived || resource.status !== "archived") {
    throw new Error("archive: archived/status was not persisted.");
  }

  await expectError("archived save blocked", "PUT", `/api/microflows/${state.createdId}/schema`, {
    baseVersion: resource.schemaId,
    schema: makeSchema(resource, "Archived Save"),
  }, 409, "MICROFLOW_ARCHIVED");

  resource = await expectOk("restore", "POST", `/api/microflows/${state.createdId}/restore`);
  if (resource.archived || resource.status !== "draft") {
    throw new Error("restore: archived/status was not persisted.");
  }

  await expectOk("delete duplicate", "DELETE", `/api/microflows/${state.duplicateId}`);
  state.duplicateId = undefined;
  await expectOk("delete resource", "DELETE", `/api/microflows/${state.createdId}`);
  const deletedId = state.createdId;
  state.createdId = undefined;
  await expectError("get deleted/not found", "GET", `/api/microflows/${deletedId}`, undefined, 404, "MICROFLOW_NOT_FOUND");
}

main()
  .catch(async error => {
    console.error(`FAIL ${error instanceof Error ? error.message : String(error)}`);
    await cleanup();
    process.exitCode = 1;
  });
