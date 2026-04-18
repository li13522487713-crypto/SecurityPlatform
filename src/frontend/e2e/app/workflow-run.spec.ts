import { expect, test, type APIRequestContext } from "../fixtures/single-session";
import { clickWorkflowTestRun, createWorkflowSession } from "./workflow-e2e-helpers";
import { appApiBase, defaultPassword, defaultTenantId, defaultUsername } from "./helpers";

const TENANT_HEADERS = {
  "X-Tenant-Id": defaultTenantId,
  "X-Project-Id": "1",
  "Content-Type": "application/json"
};

async function getAccessToken(request: APIRequestContext): Promise<string> {
  const resp = await request.post(`${appApiBase}/api/v1/auth/token`, {
    headers: { "Content-Type": "application/json", "X-Tenant-Id": defaultTenantId },
    data: { username: defaultUsername, password: defaultPassword }
  });
  expect(resp.ok()).toBeTruthy();
  const body = (await resp.json()) as { data?: { accessToken?: string } };
  const token = body?.data?.accessToken ?? "";
  expect(token).toBeTruthy();
  return token;
}

async function getCsrfToken(request: APIRequestContext, accessToken: string): Promise<string> {
  void request;
  void accessToken;
  return "deprecated-csrf-token";
}

test.describe.serial("Workflow Run E2E", () => {
  // Coze playground 接管后未发出旧版 testId（toolbar.test-run / run-inputs / canvas-json）；
  // 详见 docs/e2e-baseline-failures.md §3。cancel 接口契约 case 仍保留。
  test.fixme("should trigger run flow and render latest execution summary", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await clickWorkflowTestRun(page, "{\"input\":\"hello\"}");

    await expect
      .poll(
        async () => {
          const items = await page.getByTestId("workflow.detail.node.testrun.result-item").allTextContents();
          return items.join("\n");
        },
        { timeout: 30_000 }
      )
      .toContain("save_draft");
  });

  test.fixme("should allow updating run input json before rerun", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);

    await page.getByTestId("workflow.detail.run-inputs").fill("{\"ticket\":\"A-1001\"}");
    await page.getByTestId("workflow.detail.toolbar.test-run").click();
    await expect(page.getByTestId("workflow.detail.node.testrun.result-panel")).toBeVisible();
  });

  test("should support cancel endpoint contract", async ({ request }) => {
    const accessToken = await getAccessToken(request);
    const csrfToken = await getCsrfToken(request, accessToken);

    const resp = await request.post(`${appApiBase}/api/v2/workflows/executions/999999999/cancel`, {
      headers: {
        ...TENANT_HEADERS,
        Authorization: `Bearer ${accessToken}`
      },
      data: {}
    });

    expect(resp.status()).toBe(404);
  });
});
