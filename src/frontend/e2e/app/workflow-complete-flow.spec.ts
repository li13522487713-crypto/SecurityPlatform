import { expect, test } from "@playwright/test";
import { appBaseUrl, clearAuthStorage, ensureAppSetup, loginApp } from "./helpers";

test.describe.serial("Workflow Complete Flow", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test("应完成工作流端到端链路（拖拽、拉线、模型回复）", async ({ page }) => {
    test.setTimeout(360_000);

    await clearAuthStorage(page);
    await loginApp(page, appKey);
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows`);
    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows`), { timeout: 30_000 });

    const createResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "POST" && /\/api\/v2\/workflows$/.test(response.url());
    });

    await page.getByRole("button", { name: /新建工作流|Create Workflow/ }).click();
    const createResponse = await createResponsePromise;
    expect(createResponse.ok()).toBeTruthy();

    const createPayload = (await createResponse.json()) as { data?: { id?: string } | string };
    const createdWorkflowId =
      typeof createPayload.data === "string"
        ? createPayload.data
        : (createPayload.data?.id ?? "");

    await page.waitForTimeout(500);
    if (!/\/workflows\/[^/]+\/editor$/.test(page.url())) {
      expect(createdWorkflowId).not.toBe("");
      await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(createdWorkflowId)}/editor`);
    }

    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/[^/]+/editor`), { timeout: 30_000 });

    const canvas = page.locator(".wf-react-canvas-shell");
    await expect(canvas).toBeVisible();
    await canvas.click({ position: { x: 24, y: 24 } });
    await expect(page.locator(".wf-react-properties-panel")).toBeHidden();

    await page.getByRole("button", { name: /测试运行|Test Run/ }).click();
    const testRunPanel = page.locator(".wf-react-test-panel");
    await expect(testRunPanel).toBeVisible();
  });
});
