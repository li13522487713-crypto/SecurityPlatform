import { expect, test } from "../fixtures/single-session";
import { ensureAppSetup, navigateBySidebar } from "./helpers";

test.describe.serial("@smoke Workflow Orchestration", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("应可进入工作流列表页并看到列表区域", async ({ page }) => {
    // 当前 UI：/org/<org>/workspaces/<ws>/workflows 在没有具体 workflowId 时
    // 渲染 WorkspaceStudioRoute（focus=workflow），实际页面 testId 为 app-develop-page。
    await navigateBySidebar(page, "workflows", {
      pageTestId: "app-develop-page",
      urlPattern: /\/org\/[^/]+\/workspaces\/[^/]+\/workflows(?:\?.*)?$/
    });
    // "创建" 入口在 develop 顶栏的 dropdown 内，testId=app-develop-create-menu
    await expect(page.getByTestId("app-develop-create-menu")).toBeVisible();
  });
});

