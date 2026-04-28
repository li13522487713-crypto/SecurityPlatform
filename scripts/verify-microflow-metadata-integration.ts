const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";

type Check = { name: string; run: () => Promise<void> };

async function request(path: string, init?: RequestInit): Promise<{ status: number; body: any }> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Workspace-Id": workspaceId,
      "X-Tenant-Id": tenantId,
      "X-User-Id": userId,
      ...init?.headers,
    },
  });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : undefined };
}

function expectEnvelope(result: { status: number; body: any }, expectedSuccess = true): any {
  if (typeof result.body?.success !== "boolean") {
    throw new Error(`响应不是 MicroflowApiResponse envelope，HTTP ${result.status}`);
  }
  if (result.body.success !== expectedSuccess) {
    throw new Error(`success=${result.body.success}，期望 ${expectedSuccess}`);
  }
  return result.body.data;
}

function expectArray(value: unknown, name: string): void {
  if (!Array.isArray(value)) {
    throw new Error(`${name} 不是数组`);
  }
}

const checks: Check[] = [
  { name: "metadata health", run: async () => expectEnvelope(await request("/api/microflow-metadata/health")) },
  {
    name: "catalog shape",
    run: async () => {
      const catalog = expectEnvelope(await request("/api/microflow-metadata"));
      expectArray(catalog.modules, "modules");
      expectArray(catalog.entities, "entities");
      expectArray(catalog.enumerations, "enumerations");
      expectArray(catalog.microflows, "microflows");
      expectArray(catalog.pages, "pages");
      expectArray(catalog.workflows, "workflows");
    },
  },
  { name: "entity Sales.Order", run: async () => expectEnvelope(await request("/api/microflow-metadata/entities/Sales.Order")) },
  { name: "enumeration Sales.OrderStatus", run: async () => expectEnvelope(await request("/api/microflow-metadata/enumerations/Sales.OrderStatus")) },
  { name: "microflow refs", run: async () => expectArray(expectEnvelope(await request("/api/microflow-metadata/microflows")), "microflows") },
  {
    name: "includeSystem=false filters System.User",
    run: async () => {
      const catalog = expectEnvelope(await request("/api/microflow-metadata?includeSystem=false"));
      if (catalog.entities.some((entity: any) => entity.qualifiedName === "System.User")) {
        throw new Error("includeSystem=false 仍返回 System.User");
      }
    },
  },
  {
    name: "unknown entity error",
    run: async () => {
      const result = await request("/api/microflow-metadata/entities/Unknown.Entity");
      if (result.status !== 404 || result.body?.error?.code !== "MICROFLOW_METADATA_NOT_FOUND") {
        throw new Error(`期望 404 MICROFLOW_METADATA_NOT_FOUND，实际 ${result.status} ${result.body?.error?.code}`);
      }
    },
  },
  {
    name: "unknown enumeration error",
    run: async () => {
      const result = await request("/api/microflow-metadata/enumerations/Unknown.Enum");
      if (result.status !== 404 || result.body?.error?.code !== "MICROFLOW_METADATA_NOT_FOUND") {
        throw new Error(`期望 404 MICROFLOW_METADATA_NOT_FOUND，实际 ${result.status} ${result.body?.error?.code}`);
      }
    },
  },
];

async function main(): Promise<void> {
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
}

void main();
