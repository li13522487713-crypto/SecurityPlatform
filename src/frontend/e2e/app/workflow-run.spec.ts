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
  const resp = await request.get(`${appApiBase}/api/v1/secure/antiforgery`, {
    headers: { ...TENANT_HEADERS, Authorization: `Bearer ${accessToken}` }
  });
  expect(resp.ok()).toBeTruthy();
  const body = (await resp.json()) as { data?: { token?: string; Token?: string } };
  const token = body?.data?.token ?? body?.data?.Token ?? "";
  expect(token).toBeTruthy();
  return token;
}

test.describe.serial("Workflow Run E2E", () => {
  test("should open test-run panel and trigger run flow", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await clickWorkflowTestRun(page, "{\"input\":\"hello\"}");

    const problemPanel = page.locator(".wf-react-problem-panel");
    await expect
      .poll(
        async () => {
          if ((await page.getByTestId("workflow.detail.node.testrun.result-item").count()) > 0) {
            return "result";
          }
          if (await problemPanel.isVisible()) {
            return "problem";
          }
          return "pending";
        },
        { timeout: 30_000 }
      )
      .not.toBe("pending");
  });

  test("should open single-node debug panel", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);

    await page.getByTestId("workflow.detail.toolbar.debug").click();
    const debugPanel = page.locator(".wf-react-debug-panel");
    await expect(debugPanel).toBeVisible();
  });

  test("should support cancel endpoint contract", async ({ request }) => {
    const accessToken = await getAccessToken(request);
    const csrfToken = await getCsrfToken(request, accessToken);

    const resp = await request.post(`${appApiBase}/api/v2/workflows/executions/999999999/cancel`, {
      headers: {
        ...TENANT_HEADERS,
        Authorization: `Bearer ${accessToken}`,
        "X-CSRF-TOKEN": csrfToken,
        "Idempotency-Key": `e2e-workflow-run-cancel-${Date.now()}`
      },
      data: {}
    });

    expect([400, 404]).toContain(resp.status());
  });
});

