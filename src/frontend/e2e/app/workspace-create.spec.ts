import { expect, test } from "../fixtures/single-session";
import type { APIRequestContext } from "@playwright/test";
import { defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

const platformApiBase = "http://127.0.0.1:5002";

function authHeaders(accessToken: string): Record<string, string> {
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${accessToken}`,
    "X-Tenant-Id": defaultTenantId
  };
}

async function loginPlatformAccessToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${platformApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: "admin",
      password: "P@ssw0rd!"
    }
  });
  expect(response.ok()).toBeTruthy();
  const payload = (await response.json()) as {
    data?: { accessToken?: string };
  };
  const accessToken = String(payload?.data?.accessToken ?? "").trim();
  expect(accessToken).not.toBe("");
  return accessToken;
}

async function ensureWorkspaceId(request: APIRequestContext, accessToken: string): Promise<string> {
  const listResponse = await request.get(`${platformApiBase}/api/v1/workspaces?page_num=1&page_size=20`, {
    headers: authHeaders(accessToken)
  });
  expect(listResponse.ok()).toBeTruthy();
  const listPayload = (await listResponse.json()) as {
    data?: { workspaces?: Array<{ id?: string | number }> };
  };
  const existingWorkspaceId = String(listPayload?.data?.workspaces?.[0]?.id ?? "").trim();
  if (existingWorkspaceId) {
    return existingWorkspaceId;
  }

  const createResponse = await request.post(`${platformApiBase}/api/v1/workspaces`, {
    headers: authHeaders(accessToken),
    data: {
      name: uniqueName("e2e-workspace"),
      description: "created by playwright e2e"
    }
  });
  expect(createResponse.ok()).toBeTruthy();
  const createPayload = (await createResponse.json()) as {
    success?: boolean;
    code?: number | string;
    data?: { id?: string | number; workspace_id?: string | number };
  };
  const success = createPayload.success === true || Number(createPayload.code) === 0;
  expect(success).toBeTruthy();
  const workspaceId = String(createPayload.data?.id ?? createPayload.data?.workspace_id ?? "").trim();
  expect(workspaceId).not.toBe("");
  return workspaceId;
}

test.describe.serial("@smoke Workspace Create", () => {
  test.beforeAll(async ({ request }) => {
    await ensureAppSetup(request);
  });

  test("create app and create agent with workspaceId should succeed", async ({ request }) => {
    test.setTimeout(180_000);

    const accessToken = await loginPlatformAccessToken(request);
    const workspaceId = await ensureWorkspaceId(request, accessToken);

    const createAppResponse = await request.post(`${platformApiBase}/api/v1/workspace-ide/apps`, {
      headers: authHeaders(accessToken),
      data: {
        name: uniqueName("e2e-app"),
        description: "created by playwright e2e",
        workspaceId
      }
    });
    expect(createAppResponse.ok()).toBeTruthy();
    const createAppPayload = (await createAppResponse.json()) as {
      success?: boolean;
      data?: { appId?: string | number; workflowId?: string | number };
    };
    expect(createAppPayload.success).toBeTruthy();
    const appId = String(createAppPayload.data?.appId ?? "").trim();
    const workflowId = String(createAppPayload.data?.workflowId ?? "").trim();
    expect(appId).not.toBe("");
    expect(workflowId).not.toBe("");

    const createAgentResponse = await request.post(`${platformApiBase}/api/v1/ai-assistants`, {
      headers: authHeaders(accessToken),
      data: {
        name: uniqueName("e2e-agent"),
        description: "created by playwright e2e",
        systemPrompt: "You are a helpful agent.",
        enableMemory: true,
        enableShortTermMemory: true,
        enableLongTermMemory: true,
        longTermMemoryTopK: 3,
        workspaceId: Number(workspaceId)
      }
    });
    const createAgentPayload = (await createAgentResponse.json()) as {
      success?: boolean;
      data?: { id?: string | number; Id?: string | number };
    };
    expect(
      createAgentResponse.ok(),
      `createAgent failed: status=${createAgentResponse.status()} payload=${JSON.stringify(createAgentPayload)}`
    ).toBeTruthy();
    expect(createAgentPayload.success).toBeTruthy();
    const agentId = String(createAgentPayload.data?.id ?? createAgentPayload.data?.Id ?? "").trim();
    expect(agentId).not.toBe("");
  });
});
