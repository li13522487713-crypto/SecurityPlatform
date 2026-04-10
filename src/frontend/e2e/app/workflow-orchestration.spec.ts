import { expect, test } from "@playwright/test";
import { appBaseUrl, clearAuthStorage, ensureAppSetup, loginApp } from "./helpers";

test.describe.serial("@smoke Workflow Orchestration", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
    await loginApp(page, appKey);
  });

  test("应可进入工作流列表页并看到列表区域", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows`);
    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows`), { timeout: 30_000 });
    await expect(page.locator("body")).toContainText(/工作流|Workflow/);
  });
});
