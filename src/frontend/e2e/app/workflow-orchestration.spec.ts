import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup } from "./helpers";

test.describe.serial("@smoke Workflow Orchestration", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("应可进入工作流列表页并看到列表区域", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows`);
    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows`), { timeout: 30_000 });
    await expect(page.locator("body")).toContainText(/工作流|Workflow/);
  });
});

