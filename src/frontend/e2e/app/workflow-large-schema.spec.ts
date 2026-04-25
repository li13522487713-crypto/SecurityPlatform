import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { expect, test } from "../fixtures/single-session";
import {
  appApiBase,
  defaultPassword,
  defaultTenantId,
  defaultUsername
} from "./helpers";
import {
  expectWorkflowEditorReady,
  loginToWorkflowList,
  openWorkflowEditor,
  workflowConnectionLocator,
  workflowNodeLocator
} from "./workflow-e2e-helpers";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const longChainFixturePath = path.resolve(
  __dirname,
  "../../packages/workflow/__fixtures__/workflow-large/long-chain-30.json"
);

async function getAccessToken(request: Parameters<typeof test>[0]["request"]): Promise<string> {
  const response = await request.post(`${appApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });

  expect(response.ok()).toBeTruthy();
  const payload = (await response.json()) as { data?: { accessToken?: string } };
  const token = String(payload.data?.accessToken ?? "").trim();
  expect(token).not.toBe("");
  return token;
}

function parseWorkspaceIdFromUrl(url: string): string {
  const pathname = new URL(url).pathname;
  const match = pathname.match(/^\/workspace\/([^/]+)\/resources\/workflows(?:\/|$)/);
  expect(match).toBeTruthy();
  return decodeURIComponent((match as RegExpMatchArray)[1]);
}

test.describe.serial("@workflow-large-schema Workflow large schema", () => {
  test("30 节点长链应可保存、刷新回显并发起试运行", async ({ page, request, ensureLoggedInSession }) => {
    const appKey = await loginToWorkflowList(page, request, ensureLoggedInSession);
    const workspaceId = parseWorkspaceIdFromUrl(page.url());
    const token = await getAccessToken(request);

    const createResponse = await request.post(`${appApiBase}/api/app-web/workflow-sdk/create`, {
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": defaultTenantId
      },
      data: {
        name: `E2E Large Schema ${Date.now()}`,
        desc: "Large schema fixture",
        icon_uri: "",
        space_id: workspaceId,
        flow_mode: 0
      }
    });
    expect(createResponse.ok()).toBeTruthy();
    const createPayload = (await createResponse.json()) as { data?: { workflow_id?: string | number } };
    const workflowId = String(createPayload.data?.workflow_id ?? "").trim();
    expect(workflowId).not.toBe("");

    const fixture = JSON.parse(readFileSync(longChainFixturePath, "utf8")) as {
      nodes: unknown[];
      edges: unknown[];
    };
    const saveResponse = await request.post(`${appApiBase}/api/app-web/workflow-sdk/save`, {
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": defaultTenantId
      },
      data: {
        workflow_id: workflowId,
        space_id: workspaceId,
        submit_commit_id: workflowId,
        schema: JSON.stringify(fixture)
      }
    });
    expect(saveResponse.ok()).toBeTruthy();

    await openWorkflowEditor(page, appKey, workflowId);
    await expectWorkflowEditorReady(page);
    await page.reload();
    await expectWorkflowEditorReady(page);

    await expect.poll(async () => workflowNodeLocator(page).count(), { timeout: 30_000 }).toBe(fixture.nodes.length);
    await expect.poll(async () => workflowConnectionLocator(page).count(), { timeout: 30_000 }).toBeGreaterThanOrEqual(
      fixture.edges.length
    );

    const runResponse = await request.post(`${appApiBase}/api/workflow_api/test_run`, {
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": defaultTenantId
      },
      data: {
        workflow_id: workflowId,
        space_id: workspaceId,
        input: { query: "hello" }
      }
    });
    expect(runResponse.ok()).toBeTruthy();
    const runPayload = (await runResponse.json()) as { data?: { execute_id?: string | number }; msg?: string };
    expect(JSON.stringify(runPayload)).not.toContain("NODE_EXECUTOR_NOT_REGISTERED");
    expect(String(runPayload.data?.execute_id ?? "")).not.toBe("");
  });
});
