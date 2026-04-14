import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  navigateBySidebar,
  seedLocale
} from "./helpers";

test.describe.serial("@smoke App Navigation", () => {
  let appKey = "";

  test.setTimeout(120_000);

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
    await expect(page.getByTestId("app-sidebar")).toBeVisible();
  });

  test("sidebar should navigate to canonical pages", async ({ page }) => {
    const navigationCases = [
      { itemKey: "develop", pageTestId: "app-develop-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/develop(?:\\?.*)?$`) },
      { itemKey: "agents", pageTestId: "app-studio-assistants-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/assistants(?:\\?.*)?$`) },
      { itemKey: "workflows", pageTestId: "app-workflows-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/work_flow(?:\\?.*)?$`) },
      { itemKey: "projects", pageTestId: "app-studio-apps-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/apps(?:\\?.*)?$`) },
      { itemKey: "chatflows", pageTestId: "app-chatflows-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/chat_flow(?:\\?.*)?$`) },
      { itemKey: "library", pageTestId: "app-library-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/library(?:\\?.*)?$`) },
      { itemKey: "plugins", pageTestId: "app-studio-plugins-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/plugins(?:\\?.*)?$`) },
      { itemKey: "agent-chat", pageTestId: "app-agent-chat-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/chat(?:\\?.*)?$`) },
      { itemKey: "model-configs", pageTestId: "app-model-configs-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/model-configs(?:\\?.*)?$`) },
      { itemKey: "ai-assistant", pageTestId: "app-ai-assistant-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/assistant-tools(?:\\?.*)?$`) },
      { itemKey: "data", pageTestId: "app-studio-data-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/data(?:\\?.*)?$`) },
      { itemKey: "knowledge-bases", pageTestId: "app-studio-knowledge-bases-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases(?:\\?.*)?$`) },
      { itemKey: "databases", pageTestId: "app-studio-databases-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/databases(?:\\?.*)?$`) },
      { itemKey: "variables", pageTestId: "app-studio-variables-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/studio/variables(?:\\?.*)?$`) },
      { itemKey: "organization-overview", pageTestId: "app-organization-overview-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/overview(?:\\?.*)?$`) },
      { itemKey: "users", pageTestId: "app-users-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/users(?:\\?.*)?$`) },
      { itemKey: "roles", pageTestId: "app-roles-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/roles(?:\\?.*)?$`) },
      { itemKey: "departments", pageTestId: "app-departments-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/departments(?:\\?.*)?$`) },
      { itemKey: "positions", pageTestId: "app-positions-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/positions(?:\\?.*)?$`) },
      { itemKey: "approval", pageTestId: "app-approval-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/approval(?:\\?.*)?$`) },
      { itemKey: "reports", pageTestId: "app-reports-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/reports(?:\\?.*)?$`) },
      { itemKey: "dashboards", pageTestId: "app-dashboards-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/dashboards(?:\\?.*)?$`) },
      { itemKey: "visualization", pageTestId: "app-visualization-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/visualization(?:\\?.*)?$`) },
      { itemKey: "settings", pageTestId: "app-settings-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/settings(?:\\?.*)?$`) },
      { itemKey: "explore-plugins", pageTestId: "app-explore-plugins-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/explore/plugin(?:\\?.*)?$`) },
      { itemKey: "explore-templates", pageTestId: "app-explore-templates-page", urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/explore/template(?:\\?.*)?$`) }
    ];

    for (const navigationCase of navigationCases) {
      await navigateBySidebar(page, navigationCase.itemKey, {
        pageTestId: navigationCase.pageTestId,
        urlPattern: navigationCase.urlPattern
      });
      await expectNoI18nKeyLeak(page, navigationCase.pageTestId);
    }
  });

  test("header user menu should open and route profile", async ({ page }) => {
    await page.getByTestId("app-primary-item-admin").click();
    await page.getByTestId("app-header-user-menu").click();
    await page.getByTestId("app-header-menu-profile").click();
    await expect(page).toHaveURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/profile(?:\\?.*)?$`));
    await expect(page.getByTestId("app-profile-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "app-profile-page");
  });

  test("app navigation should render in zh-CN", async ({ page }) => {
    await seedLocale(page, "zh-CN");
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("AI 工作空间");
    await expect(page.getByTestId("app-shell-header-subtitle")).toContainText("统一完成创作、运行与团队协作");
    await expect(page.getByText("创作与构建")).toBeVisible();
    await expect(page.getByText("资源与运行")).toBeVisible();
    await expect(page.getByText("团队与治理")).toBeVisible();
    await expect(page.getByTestId("app-sidebar-section-more-workspace-build")).toContainText("更多");
    await expect(page.getByTestId("app-sidebar-section-more-workspace-resources")).toContainText("更多");
    await expect(page.getByTestId("app-sidebar-section-more-workspace-governance")).toContainText("更多");
    await expect(page.getByTestId("app-sidebar-item-develop")).toContainText("开发台");
    await expect(page.getByTestId("app-sidebar-item-agents")).toContainText("智能体");
    await expect(page.getByTestId("app-sidebar-item-workflows")).toContainText("工作流");
    await expect(page.getByTestId("app-sidebar-item-library")).toContainText("资源库");
    await expect(page.getByTestId("app-sidebar-item-agent-chat")).toContainText("Agent 对话");
    await expect(page.getByTestId("app-sidebar-item-model-configs")).toContainText("模型配置");
    await expect(page.getByTestId("app-sidebar-item-plugins")).toContainText("插件");
    await expect(page.getByTestId("app-sidebar-item-projects")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-chatflows")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-ai-assistant")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-data")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-knowledge-bases")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-databases")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-variables")).toHaveCount(0);
    await expect(page.getByTestId("app-sidebar-item-organization-overview")).toContainText("组织概览");
    await expect(page.getByTestId("app-sidebar-item-approval")).toHaveCount(0);

    await page.getByTestId("app-sidebar-section-more-workspace-build").click();
    await expect(page.getByTestId("app-sidebar-item-projects")).toContainText("应用");
    await expect(page.getByTestId("app-sidebar-item-chatflows")).toContainText("对话流");

    await page.getByTestId("app-sidebar-section-more-workspace-resources").click();
    await expect(page.getByTestId("app-sidebar-item-ai-assistant")).toContainText("AI 助手");
    await expect(page.getByTestId("app-sidebar-item-data")).toContainText("数据");
    await expect(page.getByTestId("app-sidebar-item-knowledge-bases")).toContainText("知识库");
    await expect(page.getByTestId("app-sidebar-item-databases")).toContainText("数据库");
    await expect(page.getByTestId("app-sidebar-item-variables")).toContainText("变量");

    await page.getByTestId("app-sidebar-section-more-workspace-governance").click();
    await expect(page.getByTestId("app-sidebar-item-approval")).toContainText("审批工作台");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("设置");

    await page.getByTestId("app-primary-item-admin").click();
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("团队协作");
    await expect(page.getByTestId("app-shell-header-subtitle")).toContainText("成员、权限");
    await expect(page.getByText("组织与权限")).toBeVisible();
    await expect(page.getByText("运营与监控")).toBeVisible();
    await expect(page.getByText("个人与设置")).toBeVisible();
    await expect(page.getByTestId("app-sidebar-item-users")).toContainText("用户管理");
    await expect(page.getByTestId("app-sidebar-item-roles")).toContainText("角色管理");
    await expect(page.getByTestId("app-sidebar-item-departments")).toContainText("部门管理");
    await expect(page.getByTestId("app-sidebar-item-positions")).toContainText("职位管理");
    await expect(page.getByTestId("app-sidebar-item-approval")).toContainText("审批工作台");
    await expect(page.getByTestId("app-sidebar-item-reports")).toContainText("报表管理");
    await expect(page.getByTestId("app-sidebar-item-dashboards")).toContainText("仪表盘管理");
    await expect(page.getByTestId("app-sidebar-item-visualization")).toContainText("运行监控");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("设置");

    await page.getByTestId("app-primary-item-explore").click();
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("插件发现");
    await expect(page.getByTestId("app-shell-header-subtitle")).toContainText("插件能力");
    await expect(page.getByText("探索与模板")).toBeVisible();
    await expect(page.getByTestId("app-sidebar-item-explore-plugins")).toContainText("插件商店");
    await expect(page.getByTestId("app-sidebar-item-explore-templates")).toContainText("模板商店");
  });
});
