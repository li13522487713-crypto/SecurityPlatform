import { expect, test } from "../fixtures/single-session";
import { selectWorkspacePath } from "@atlas/app-shell-shared";
import { appBaseUrl, ensureAppSetup, navigateBySidebar } from "./helpers";

function apiOk<T>(data: T) {
  return {
    success: true,
    code: "SUCCESS",
    message: "OK",
    traceId: "studio-dashboard-e2e",
    data
  };
}

test.describe.serial("Studio Dashboard", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should render dashboard alias route, quick start, model guard banner, and recent resources", async ({ page }) => {
    const summaryRoute = /\/api\/v1\/workspace-ide\/summary(?:\?.*)?$/;
    const modelConfigsRoute = /\/api\/v1\/model-configs(?:\?.*)?$/;
    const resourcesRoute = /\/api\/v1\/workspace-ide\/resources(?:\?.*)?$/;
    const dashboardStatsRoute = /\/api\/v1\/workspace-ide\/dashboard-stats(?:\?.*)?$/;

    const summaryHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            appCount: 2,
            agentCount: 3,
            workflowCount: 4,
            chatflowCount: 1,
            pluginCount: 5,
            knowledgeBaseCount: 2,
            databaseCount: 1,
            favoriteCount: 0,
            recentCount: 2
          })
        )
      });
    };

    const modelConfigsHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            items: [],
            total: 0,
            pageIndex: 1,
            pageSize: 50
          })
        )
      });
    };

    const resourcesHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            items: [
              {
                resourceType: "agent",
                resourceId: "agent-001",
                name: "安全助手",
                description: "最近打开的智能体",
                icon: null,
                status: "Draft",
                publishStatus: "published",
                updatedAt: "2026-04-14T08:00:00Z",
                isFavorite: false,
                lastEditedAt: "2026-04-14T08:00:00Z",
                entryRoute: "/studio/assistants/agent-001",
                badge: "v2"
              },
              {
                resourceType: "workflow",
                resourceId: "workflow-001",
                name: "告警分诊工作流",
                description: "最近访问的工作流",
                icon: null,
                status: "Published",
                publishStatus: "published",
                updatedAt: "2026-04-14T08:05:00Z",
                isFavorite: false,
                lastEditedAt: "2026-04-14T08:05:00Z",
                entryRoute: "/workflows/workflow-001/editor",
                badge: "v5"
              }
            ],
            total: 2,
            pageIndex: 1,
            pageSize: 10
          })
        )
      });
    };

    const dashboardStatsHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            agentCount: 3,
            appCount: 2,
            workflowCount: 4,
            enabledModelCount: 0,
            pluginCount: 5,
            knowledgeBaseCount: 2,
            pendingPublishItems: [
              {
                resourceType: "app",
                resourceId: "app-001",
                resourceName: "威胁分析应用",
                updatedAt: "2026-04-14T09:00:00Z"
              }
            ],
            recentActivities: [
              {
                resourceType: "agent",
                resourceId: "agent-001",
                name: "安全助手",
                description: "最近打开的智能体",
                icon: null,
                status: "Draft",
                publishStatus: "published",
                updatedAt: "2026-04-14T08:00:00Z",
                isFavorite: false,
                lastEditedAt: "2026-04-14T08:00:00Z",
                entryRoute: "/studio/assistants/agent-001",
                badge: "v2"
              },
              {
                resourceType: "workflow",
                resourceId: "workflow-001",
                name: "告警分诊工作流",
                description: "最近访问的工作流",
                icon: null,
                status: "Published",
                publishStatus: "published",
                updatedAt: "2026-04-14T08:05:00Z",
                isFavorite: false,
                lastEditedAt: "2026-04-14T08:05:00Z",
                entryRoute: "/workflows/workflow-001/editor",
                badge: "v5"
              }
            ]
          })
        )
      });
    };

    await page.route(summaryRoute, summaryHandler);
    await page.route(modelConfigsRoute, modelConfigsHandler);
    await page.route(resourcesRoute, resourcesHandler);
    await page.route(dashboardStatsRoute, dashboardStatsHandler);

    try {
      await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
      const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
      if (await matchedWorkspaceButton.count()) {
        await matchedWorkspaceButton.click();
      } else {
        await page.locator('[data-testid^="coze-select-workspace-"]').first().click();
      }
      await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
      await navigateBySidebar(page, "home", { pageTestId: "coze-home-page" });

      await expect(page.getByTestId("coze-home-page")).toBeVisible();
      await expect(page.getByTestId("coze-home-banner-cta-create")).toBeVisible();
    } finally {
      await page.unroute(summaryRoute, summaryHandler);
      await page.unroute(modelConfigsRoute, modelConfigsHandler);
      await page.unroute(resourcesRoute, resourcesHandler);
      await page.unroute(dashboardStatsRoute, dashboardStatsHandler);
    }
  });
});
