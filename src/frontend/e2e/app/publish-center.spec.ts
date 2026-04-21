import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup } from "./helpers";

function apiOk<T>(data: T) {
  return {
    success: true,
    code: "SUCCESS",
    message: "OK",
    traceId: "publish-center-e2e",
    data
  };
}

test.describe.serial("Publish Center", () => {
  test.fixme("旧壳 Publish Center 页面入口已下线，待新壳发布中心场景补齐后恢复。");
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should render publish catalog, API access panel, and token management", async ({ page }) => {
    const publishItemsRoute = /\/api\/v1\/workspace-ide\/publish-center\/items(?:\?.*)?$/;
    const publishItemsHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk([
            {
              resourceType: "agent",
              resourceId: "agent-001",
              resourceName: "SOC 智能体",
              currentVersion: 1,
              draftVersion: 2,
              lastPublishedAt: "2026-04-14T10:00:00Z",
              status: "outdated",
              apiEndpoint: "agents/agent-001/runtime",
              embedToken: "embed-agent-token-001"
            },
            {
              resourceType: "app",
              resourceId: "app-001",
              resourceName: "事件处置应用",
              currentVersion: 3,
              draftVersion: 3,
              lastPublishedAt: "2026-04-14T10:05:00Z",
              status: "published",
              apiEndpoint: "ai-apps/app-001/runtime"
            },
            {
              resourceType: "workflow",
              resourceId: "workflow-001",
              resourceName: "告警编排工作流",
              currentVersion: 5,
              draftVersion: 6,
              lastPublishedAt: "2026-04-14T10:10:00Z",
              status: "outdated",
              apiEndpoint: "workflows/workflow-001/runtime"
            },
            {
              resourceType: "plugin",
              resourceId: "plugin-001",
              resourceName: "威胁情报插件",
              currentVersion: 2,
              draftVersion: 2,
              lastPublishedAt: "2026-04-14T10:15:00Z",
              status: "published",
              apiEndpoint: "plugins/plugin-001/runtime"
            }
          ])
        )
      });
    };

    await page.route(publishItemsRoute, publishItemsHandler);

    try {
      await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/studio/publish-center`);

      const publishPage = page.getByTestId("studio-publish-center-page");
      await expect(publishPage).toBeVisible({ timeout: 30_000 });
      // 页面内的"发布中心"标题（避免与左侧菜单项重名）
      await expect(publishPage.getByRole("heading", { name: "发布中心" })).toBeVisible();

      await expect(publishPage.getByText("SOC 智能体").first()).toBeVisible();
      await expect(publishPage.getByText("事件处置应用").first()).toBeVisible();
      await expect(publishPage.getByText("告警编排工作流").first()).toBeVisible();
      await expect(publishPage.getByText("威胁情报插件").first()).toBeVisible();

      await publishPage
        .getByRole("button", { name: "智能体", exact: true })
        .click();
      await expect(publishPage.getByText("SOC 智能体").first()).toBeVisible();

      await page.getByRole("tab", { name: "HTTP 接入" }).click();
      await expect(page.getByTestId("studio-publish-api-access-panel")).toBeVisible();
      await expect(page.getByText("cURL 示例").first()).toBeVisible();

      await page.getByRole("tab", { name: "嵌入令牌" }).click();
      await expect(page.getByTestId("studio-publish-token-management")).toBeVisible();
      await expect(page.getByText("embed-agent-token-001")).toBeVisible();
    } finally {
      await page.unroute(publishItemsRoute, publishItemsHandler);
    }
  });
});
