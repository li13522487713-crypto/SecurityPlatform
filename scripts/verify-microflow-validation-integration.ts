const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";
const microflowId = process.env.MICROFLOW_ID ?? "mf-seed-blank";

type ValidationMode = "edit" | "save" | "publish" | "testRun";

async function request(path: string, body?: unknown): Promise<{ status: number; body: any }> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Workspace-Id": workspaceId,
      "X-Tenant-Id": tenantId,
      "X-User-Id": userId,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : undefined };
}

function expectValidationEnvelope(result: { status: number; body: any }): any {
  if (result.status !== 200 || result.body?.success !== true) {
    throw new Error(`校验 API 失败：HTTP ${result.status} ${result.body?.error?.code ?? ""}`);
  }
  const data = result.body.data;
  if (!Array.isArray(data?.issues) || typeof data.summary?.errorCount !== "number" || !data.serverValidatedAt) {
    throw new Error("ValidateMicroflowResponse shape 不符合前端契约");
  }
  for (const issue of data.issues) {
    for (const key of ["id", "severity", "code", "message", "source"]) {
      if (!issue[key]) {
        throw new Error(`issue 缺少 ${key}`);
      }
    }
  }
  return data;
}

function schema(name: string, objects: any[], flows: any[] = []) {
  return {
    schemaVersion: "1.0.0",
    id: microflowId,
    name,
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
    variables: {},
    validation: { issues: [] },
  };
}

async function validate(mode: ValidationMode, inlineSchema?: unknown) {
  return expectValidationEnvelope(await request(`/api/microflows/${encodeURIComponent(microflowId)}/validate`, {
    mode,
    includeWarnings: true,
    includeInfo: true,
    schema: inlineSchema,
  }));
}

const checks: Array<{ name: string; run: () => Promise<void> }> = [
  { name: "saved schema edit mode", run: async () => { await validate("edit"); } },
  { name: "saved schema save mode", run: async () => { await validate("save"); } },
  { name: "saved schema publish mode", run: async () => { await validate("publish"); } },
  { name: "saved schema testRun mode", run: async () => { await validate("testRun"); } },
  {
    name: "invalid schema rejects FlowGram JSON",
    run: async () => {
      const data = await validate("edit", { nodes: [], edges: [] });
      if (!data.issues.some((issue: any) => issue.code === "MF_ROOT_SCHEMA_INVALID")) {
        throw new Error("未返回 MF_ROOT_SCHEMA_INVALID");
      }
    },
  },
  {
    name: "missing start issue",
    run: async () => {
      const data = await validate("publish", schema("MissingStart", [
        { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
      ]));
      if (!data.issues.some((issue: any) => issue.code === "MF_START_MISSING")) {
        throw new Error("未返回 MF_START_MISSING");
      }
    },
  },
  {
    name: "missing action fieldPath",
    run: async () => {
      const data = await validate("testRun", schema("MissingActionField", [
        { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" },
        {
          id: "retrieve1",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          action: { id: "a1", kind: "retrieve", officialType: "Microflows$RetrieveAction", retrieveSource: { kind: "database", entityQualifiedName: "" } },
        },
        { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" },
      ], [
        { id: "f1", kind: "sequence", originObjectId: "start", destinationObjectId: "retrieve1" },
        { id: "f2", kind: "sequence", originObjectId: "retrieve1", destinationObjectId: "end" },
      ]));
      if (!data.issues.some((issue: any) => issue.objectId === "retrieve1" && issue.fieldPath === "action.retrieveSource.entityQualifiedName")) {
        throw new Error("缺少 action.retrieveSource.entityQualifiedName 字段级 issue");
      }
    },
  },
  {
    name: "metadata reference issue",
    run: async () => {
      const data = await validate("save", {
        ...schema("InvalidMetadata", []),
        parameters: [{ id: "p1", name: "order", type: { kind: "object", entityQualifiedName: "Sales.NotExists" } }],
      });
      if (!data.issues.some((issue: any) => issue.source === "metadata")) {
        throw new Error("未返回 metadata source issue");
      }
    },
  },
];

let failed = 0;
for (const check of checks) {
  try {
    await check.run();
    console.log(`PASS ${check.name}`);
  } catch (error) {
    failed += 1;
    console.error(`FAIL ${check.name}: ${error instanceof Error ? error.message : String(error)}`);
  }
}

if (failed > 0) {
  process.exitCode = 1;
}
