import { expect, test } from "../fixtures/single-session";
import { ensureAppSetup, navigateBySidebar } from "./helpers";

test.describe.serial("@smoke Workflow Orchestration", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("应可进入工作流列表页并看到列表区域", async ({ page }) => {
    // 新壳：工作流列表统一收口到 /workspace/<ws>/resources/workflows。
    await navigateBySidebar(page, "workflows", {
      pageTestId: "coze-resource-page",
      urlPattern: /\/workspace\/[^/]+\/resources\/workflows(?:\?.*)?$/
    });
    await expect(page.getByTestId("coze-resource-page")).toBeVisible();
  });
});

