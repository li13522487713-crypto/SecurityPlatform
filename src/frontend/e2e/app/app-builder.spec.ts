import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup, navigateBySidebar } from "./helpers";

const appId = "e2e-builder-app";

function apiOk<T>(data: T) {
  return {
    success: true,
    code: "SUCCESS",
    message: "OK",
    traceId: "app-builder-e2e",
    data
  };
}

async function chooseSemiOption(page: import("@playwright/test").Page, trigger: import("@playwright/test").Locator, optionText: string) {
  const selection = trigger.locator(".semi-select-selection").first();
  if (await selection.count()) {
    await selection.click();
  } else {
    await trigger.click();
  }

  const option = page.locator(".semi-select-option:visible").filter({ hasText: optionText }).first();
  await expect(option).toBeVisible({ timeout: 30_000 });
  await option.click();
}

test.describe.serial("App Builder", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should configure inputs, outputs, workflow binding, and preview run", async ({ page }) => {
    // App Builder 涉及多个 mock 路由 + 长链路交互，30s 默认 timeout 不够。
    test.setTimeout(120_000);
    const appDetailRoute = new RegExp(`/api/v1/ai-apps/${appId}(?:\\?.*)?$`);
    const builderConfigRoute = new RegExp(`/api/v1/ai-apps/${appId}/builder-config(?:\\?.*)?$`);
    const previewRunRoute = new RegExp(`/api/v1/ai-apps/${appId}/preview-run(?:\\?.*)?$`);
    const workflowsRoute = /\/api\/v2\/workflows(?:\?.*)?$/;

    const appDetailHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            id: appId,
            name: "E2E 预警处置应用",
            description: "用于校验 App Builder 主流程。",
            icon: null,
            agentId: null,
            workflowId: "wf-builder-001",
            promptTemplateId: null,
            status: "Draft",
            publishVersion: 0,
            createdAt: "2026-04-14T09:00:00Z",
            updatedAt: "2026-04-14T09:10:00Z",
            publishRecords: []
          })
        )
      });
    };

    const builderConfigHandler = async (route: import("@playwright/test").Route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(
            apiOk({
              inputs: [],
              outputs: [],
              boundWorkflowId: undefined,
              layoutMode: "form"
            })
          )
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(apiOk({}))
      });
    };

    const workflowsHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            items: [
              {
                id: "wf-builder-001",
                name: "安全事件处置流",
                description: "已发布工作流",
                status: 1,
                latestVersionNumber: 5,
                updatedAt: "2026-04-14T09:20:00Z",
                mode: 0
              }
            ],
            total: 1,
            pageIndex: 1,
            pageSize: 100
          })
        )
      });
    };

    const previewRunHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            outputs: {
              answer: "已生成处置建议：先隔离主机，再执行凭证轮换与溯源。"
            },
            trace: {
              executionId: "exec-builder-001",
              status: "Succeeded",
              startedAt: "2026-04-14T09:30:00Z",
              completedAt: "2026-04-14T09:30:03Z",
              durationMs: 3000,
              steps: [
                {
                  nodeKey: "entry_1",
                  status: "Succeeded",
                  nodeType: "Entry",
                  durationMs: 20,
                  errorMessage: null
                },
                {
                  nodeKey: "llm_1",
                  status: "Succeeded",
                  nodeType: "Llm",
                  durationMs: 2980,
                  errorMessage: null
                }
              ]
            }
          })
        )
      });
    };

    const workspaceResourcesRoute = /\/api\/v1\/workspace-ide\/resources(?:\?.*)?$/;
    const workspaceResourcesHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          apiOk({
            items: [
              {
                resourceType: "app",
                resourceId: appId,
                name: "E2E 预警处置应用",
                description: "用于校验 App Builder 主流程。",
                icon: null,
                status: "Draft",
                publishStatus: "draft",
                updatedAt: "2026-04-14T09:10:00Z",
                isFavorite: false,
                lastEditedAt: "2026-04-14T09:10:00Z",
                entryRoute: `/apps/${encodeURIComponent(appKey)}/studio/apps/${encodeURIComponent(appId)}`,
                badge: null,
                linkedWorkflowId: "wf-builder-001"
              }
            ],
            total: 1,
            pageIndex: 1,
            pageSize: 120
          })
        )
      });
    };
    const agentsRoute = /\/api\/v1\/ai-agents(?:\?.*)?$/;
    const agentsHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(apiOk({ items: [], total: 0, pageIndex: 1, pageSize: 20 }))
      });
    };
    const modelConfigsListRoute = /\/api\/v1\/model-configs(?:\?.*)?$/;
    const modelConfigsListHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(apiOk({ items: [], total: 0, pageIndex: 1, pageSize: 50 }))
      });
    };
    const workspaceOverviewRoute = /\/api\/v1\/workspace-ide\/overview(?:\?.*)?$/;
    const workspaceOverviewHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(apiOk({
          appId: appKey,
          memberCount: 1,
          roleCount: 1,
          departmentCount: 0,
          positionCount: 0,
          projectCount: 1,
          uncoveredMemberCount: 0,
          applications: []
        }))
      });
    };
    const workspaceSummaryRoute = /\/api\/v1\/workspace-ide\/summary(?:\?.*)?$/;
    const workspaceSummaryHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(apiOk({
          appCount: 1,
          agentCount: 0,
          workflowCount: 1,
          chatflowCount: 0,
          pluginCount: 0,
          knowledgeBaseCount: 0,
          databaseCount: 0,
          favoriteCount: 0,
          recentCount: 0
        }))
      });
    };

    await page.route(appDetailRoute, appDetailHandler);
    await page.route(builderConfigRoute, builderConfigHandler);
    await page.route(workflowsRoute, workflowsHandler);
    await page.route(previewRunRoute, previewRunHandler);
    await page.route(workspaceResourcesRoute, workspaceResourcesHandler);
    await page.route(agentsRoute, agentsHandler);
    await page.route(modelConfigsListRoute, modelConfigsListHandler);
    await page.route(workspaceOverviewRoute, workspaceOverviewHandler);
    await page.route(workspaceSummaryRoute, workspaceSummaryHandler);

    try {
      await navigateBySidebar(page, "develop", { pageTestId: "app-develop-page" });

      await expect(page.getByTestId("app-develop-projects-grid")).toBeVisible({ timeout: 30_000 });
      const appCard = page.getByTestId("app-develop-projects-grid").locator("article").filter({ hasText: "E2E 预警处置应用" }).first();
      await expect(appCard).toBeVisible({ timeout: 15_000 });
      await appCard.getByRole("button", { name: "详情页" }).click();

      await expect(page.getByTestId("app-studio-app-detail-page")).toBeVisible({ timeout: 30_000 });
      await expect(page.getByText("应用构建器")).toBeVisible();

      await page.getByRole("button", { name: "添加输入项" }).click();
      await page.getByPlaceholder("显示名称").fill("事件描述");
      await page.getByPlaceholder("如 userQuery").fill("incident");

      await page.getByRole("button", { name: "添加输出项" }).click();
      await page.getByPlaceholder("展示标题").fill("处置建议");
      await page.getByPlaceholder("如 answer 或 data.summary").fill("answer");

      const workflowSelect = page
        .locator(".module-studio__app-builder-panel")
        .filter({ hasText: "工作流绑定" })
        .locator(".semi-select")
        .first();
      await chooseSemiOption(page, workflowSelect, "安全事件处置流");
      await expect(page.getByText("在工作流编辑器中打开")).toBeVisible();

      const saveResponsePromise = page.waitForResponse(
        (response) => builderConfigRoute.test(response.url()) && response.request().method() === "PUT"
      );
      await page.getByRole("button", { name: "保存配置" }).click();
      await saveResponsePromise;

      await page.getByPlaceholder("事件描述").fill("检测到可疑 PowerShell 横向移动行为。");
      const previewResponsePromise = page.waitForResponse(
        (response) => previewRunRoute.test(response.url()) && response.request().method() === "POST"
      );
      await page.getByRole("button", { name: "运行预览" }).click();
      await previewResponsePromise;

      await expect(page.getByRole("strong").filter({ hasText: "处置建议" })).toBeVisible();
      await expect(page.getByText("已生成处置建议：先隔离主机，再执行凭证轮换与溯源。")).toBeVisible();
      await expect(page.getByText("执行轨迹")).toBeVisible();
      await expect(page.getByText("exec-builder-001")).toBeVisible();
    } finally {
      await page.unroute(appDetailRoute, appDetailHandler);
      await page.unroute(builderConfigRoute, builderConfigHandler);
      await page.unroute(workflowsRoute, workflowsHandler);
      await page.unroute(previewRunRoute, previewRunHandler);
      await page.unroute(workspaceResourcesRoute, workspaceResourcesHandler);
      await page.unroute(agentsRoute, agentsHandler);
      await page.unroute(modelConfigsListRoute, modelConfigsListHandler);
      await page.unroute(workspaceOverviewRoute, workspaceOverviewHandler);
      await page.unroute(workspaceSummaryRoute, workspaceSummaryHandler);
    }
  });
});
