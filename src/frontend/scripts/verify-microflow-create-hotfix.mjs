const baseUrl = (process.env.MICROFLOW_API_BASE_URL ?? process.env.VITE_MICROFLOW_API_BASE_URL ?? "http://localhost:5002").replace(/\/+$/u, "");
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "verify-user";
const runId = Date.now().toString(36);
const createdName = `CreateHotfix${runId}`;

let createdId;

function apiUrl(path, base = baseUrl) {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const resolvedPath = base.endsWith("/api") && normalizedPath.startsWith("/api/")
    ? normalizedPath.slice("/api".length)
    : normalizedPath;
  return `${base}${resolvedPath}`;
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

function assertTrace(response, payload, label) {
  const headerTraceId = response.headers.get("X-Trace-Id");
  const envelopeTraceId = payload?.traceId;
  const errorTraceId = payload?.error?.traceId;
  if (!headerTraceId || !envelopeTraceId || !errorTraceId) {
    throw new Error(`${label}: missing traceId in header or envelope.`);
  }
}

async function expectOk(label, method, path, body) {
  const { response, payload } = await request(method, path, body);
  assertEnvelope(payload, label);
  if (!response.ok || payload.success !== true) {
    throw new Error(`${label}: expected success, got HTTP ${response.status} ${payload?.error?.code ?? ""}`);
  }
  console.log(`PASS ${label}`);
  return payload.data;
}

async function expectError(label, method, path, body, expectedStatus, expectedCode) {
  const { response, payload } = await request(method, path, body);
  assertEnvelope(payload, label);
  assertTrace(response, payload, label);
  if (response.status !== expectedStatus || payload.success !== false || payload.error?.code !== expectedCode) {
    throw new Error(`${label}: expected ${expectedStatus}/${expectedCode}, got HTTP ${response.status} ${payload?.error?.code ?? ""}`);
  }
  console.log(`PASS ${label}`);
  return payload.error;
}

async function expectNetworkUnavailable(label) {
  const badBase = "http://127.0.0.1:59999";
  let failed = false;
  try {
    await fetch(apiUrl("/api/microflows", badBase), {
      method: "POST",
      headers: headers(true),
      body: JSON.stringify({
        workspaceId,
        input: {
          name: "NetworkUnavailableCase",
          moduleId: "verify-module",
          tags: [],
          parameters: [],
          returnType: { kind: "void" },
        },
      }),
    });
  } catch {
    failed = true;
  }
  if (!failed) {
    throw new Error(`${label}: expected network failure but request succeeded.`);
  }
  console.log(`PASS ${label}`);
}

async function cleanup() {
  if (!createdId) {
    return;
  }
  try {
    await request("DELETE", `/api/microflows/${createdId}`);
  } catch {
    // best effort
  }
}

async function main() {
  await expectOk("health", "GET", "/api/microflows/health");

  const created = await expectOk("create success", "POST", "/api/microflows", {
    workspaceId,
    input: {
      name: createdName,
      displayName: createdName,
      description: "Created by verify-microflow-create-hotfix.",
      moduleId: "verify-module",
      moduleName: "Verify",
      tags: ["verify", "create-hotfix"],
      parameters: [],
      returnType: { kind: "void" },
    },
  });
  createdId = created.id;

  await expectError("duplicate name 409", "POST", "/api/microflows", {
    workspaceId,
    input: {
      name: createdName,
      moduleId: "verify-module",
      tags: [],
      parameters: [],
      returnType: { kind: "void" },
    },
  }, 409, "MICROFLOW_NAME_DUPLICATED");

  const moduleError = await expectError("moduleId missing 422", "POST", "/api/microflows", {
    workspaceId,
    input: {
      name: `NoModule${runId}`,
      moduleId: "",
      tags: [],
      parameters: [],
      returnType: { kind: "void" },
    },
  }, 422, "MICROFLOW_VALIDATION_FAILED");
  if (!Array.isArray(moduleError.fieldErrors) || !moduleError.fieldErrors.some(item => item.fieldPath === "input.moduleId")) {
    throw new Error("moduleId missing 422: fieldErrors missing input.moduleId");
  }

  const invalidNameError = await expectError("invalid name 422", "POST", "/api/microflows", {
    workspaceId,
    input: {
      name: "_abc",
      moduleId: "verify-module",
      tags: [],
      parameters: [],
      returnType: { kind: "void" },
    },
  }, 422, "MICROFLOW_VALIDATION_FAILED");
  if (!Array.isArray(invalidNameError.fieldErrors) || !invalidNameError.fieldErrors.some(item => item.fieldPath === "input.name")) {
    throw new Error("invalid name 422: fieldErrors missing input.name");
  }

  await expectNetworkUnavailable("backend unavailable network failure");
}

main()
  .catch(async error => {
    console.error(`FAIL ${error instanceof Error ? error.message : String(error)}`);
    await cleanup();
    process.exitCode = 1;
  })
  .finally(async () => {
    await cleanup();
  });
