import { expect, test } from "../fixtures/single-session";
import { ensureAppSetup, navigateBySidebar } from "./helpers";

test.describe.serial("@smoke Workflow Orchestration", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("应可进入工作流列表页并看到列表区域", async ({ page }) => {
    await navigateBySidebar(page, "workflows", {
      pageTestId: "app-workflows-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows(?:\\?.*)?$`)
    });
    await expect(page.getByTestId("app-workflows-table")).toBeVisible();
  });
});

