import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
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

function read(relativePath: string): string {
  return readFileSync(join(root, relativePath), "utf8");
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function assertIncludes(file: string, needles: string[]): void {
  const content = read(file);
  for (const needle of needles) {
    assert(content.includes(needle), `${file} missing ${needle}`);
  }
}

function makeId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function schemaWithAction(name: string, action: Json, errorHandler = false): Json {
  const objects: Json[] = [
    { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
    { id: "action", kind: "actionActivity", officialType: "Microflows$ActionActivity", action },
    { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
  ];
  const flows: Json[] = [
    { id: "f-start-action", kind: "sequence", originObjectId: "start", destinationObjectId: "action" },
    { id: "f-action-end", kind: "sequence", originObjectId: "action", destinationObjectId: "end" },
  ];
  if (errorHandler) {
    objects.push({ id: "error-log", kind: "actionActivity", officialType: "Microflows$ActionActivity", action: {
      id: "log-error",
      kind: "logMessage",
      level: "warning",
      template: { text: "latest={0}", arguments: ["$latestHttpResponse/statusCode"] },
      includeTraceId: true,
    } });
    flows.push({ id: "f-action-error", kind: "sequence", originObjectId: "action", destinationObjectId: "error-log", isErrorHandler: true, editor: { edgeKind: "errorHandler" } });
    flows.push({ id: "f-error-end", kind: "sequence", originObjectId: "error-log", destinationObjectId: "end" });
  }

  return {
    schemaVersion: "1.0.0",
    id: makeId(name),
    name,
    displayName: name,
    moduleId: "verify-module",
    parameters: [
      { id: "p-url", name: "url", dataType: "string", type: { kind: "string" }, required: false },
      { id: "p-token", name: "token", dataType: "string", type: { kind: "string" }, required: false },
    ],
    returnType: { kind: "void" },
    objectCollection: { id: "root", objects, flows },
    flows: [],
  };
}

async function api(path: string, body: unknown, expectSuccess = true): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (expectSuccess && (!response.ok || envelope?.success !== true)) {
    throw new Error(`${path} failed: HTTP ${response.status} ${text}`);
  }
  return envelope;
}

async function appHostAvailable(): Promise<boolean> {
  try {
    const response = await fetch(`${baseUrl}/internal/health/ready`, { headers });
    return response.ok;
  } catch {
    return false;
  }
}

async function runRuntimeSmoke(): Promise<void> {
  const restAction = {
    id: "rest-1",
    kind: "restCall",
    officialType: "Microflows$RestCallAction",
    request: {
      method: "POST",
      urlExpression: "$url",
      headers: [{ key: "Authorization", valueExpression: "$token" }],
      queryParameters: [{ key: "q", valueExpression: "'hello'" }],
      body: { kind: "json", expression: "'{\"ok\":true}'" },
    },
    response: {
      handling: { kind: "json", outputVariableName: "payload" },
      statusCodeVariableName: "statusCode",
      headersVariableName: "headers",
    },
  };
  const restRun = await api("/api/microflows/verify-rest/test-run", {
    schema: schemaWithAction("VerifyRestMock", restAction),
    input: { url: "https://example.com/api", token: "Bearer secret" },
    options: { allowRealHttp: false },
  });
  const session = restRun.data?.session ?? restRun.session;
  assert(session?.status === "success", "mock RestCall should succeed");
  const restFrame = session.trace.find((frame: Json) => frame.objectId === "action");
  assert(JSON.stringify(restFrame?.output ?? {}).includes("restCall"), "TraceFrame.output.restCall missing");
  assert(JSON.stringify(restFrame?.output ?? {}).includes("***redacted***"), "sensitive header should be redacted");
  assert(JSON.stringify(restFrame?.variablesSnapshot ?? {}).includes("statusCode"), "statusCode variable missing");

  const logRun = await api("/api/microflows/verify-log/test-run", {
    schema: schemaWithAction("VerifyLog", {
      id: "log-1",
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      level: "info",
      logNodeName: "VerifyLogNode",
      template: { text: "URL={0}", arguments: ["$url"] },
      includeTraceId: true,
      includeContextVariables: true,
    }),
    input: { url: "https://example.com", token: "secret" },
  });
  const logSession = logRun.data?.session ?? logRun.session;
  assert(logSession?.logs?.some((log: Json) => log.logNodeName === "VerifyLogNode" && String(log.message).includes("URL=https://example.com")), "structured LogMessage log missing");

  const errorRun = await api("/api/microflows/verify-rest-error/test-run", {
    schema: schemaWithAction("VerifyRestError", restAction, true),
    input: { url: "https://example.com/api", token: "Bearer secret" },
    options: { simulateRestError: true },
  });
  const errorSession = errorRun.data?.session ?? errorRun.session;
  assert(JSON.stringify(errorSession).includes("$latestHttpResponse"), "latestHttpResponse should be visible in error handler scope");
}

async function main(): Promise<void> {
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Actions/Http/MicroflowRuntimeHttpClient.cs", [
    "IMicroflowRuntimeHttpClient",
    "IHttpClientFactory",
    "AllowRealHttp",
    "MaxResponseBytes",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Actions/Http/MicroflowRestSecurityPolicy.cs", [
    "RuntimeRestPrivateNetworkBlocked",
    "RuntimeRestUnsupportedScheme",
    "localhost",
    "DeniedHosts",
    "AllowedHosts",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Actions/RestCallActionExecutor.cs", [
    "LatestHttpResponse",
    "TraceId",
    "StructuredFieldsJson",
    "SimulateRestError",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/Actions/LogMessageActionExecutor.cs", [
    "includeContextVariables",
    "includeTraceId",
    "LogNodeName",
    "StructuredFieldsJson",
  ]);
  assertIncludes("src/frontend/packages/mendix/mendix-microflow/src/debug/trace-types.ts", [
    "structuredFieldsJson",
    "logNodeName",
    "allowRealHttp",
  ]);

  if (await appHostAvailable()) {
    await runRuntimeSmoke();
    console.log("PASS microflow RestCall/LogMessage runtime smoke");
  } else {
    console.log("PASS static checks; SKIP runtime smoke because AppHost is not reachable");
  }
}

main().catch(error => {
  console.error("FAIL microflow RestCall/LogMessage runtime verify");
  console.error(error);
  process.exit(1);
});
