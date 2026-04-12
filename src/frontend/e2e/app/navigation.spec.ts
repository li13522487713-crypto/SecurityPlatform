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

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
    await expect(page.getByTestId("app-sidebar")).toBeVisible();
  });

  test("sidebar should navigate to canonical pages", async ({ page }) => {
    await navigateBySidebar(page, "develop", {
      pageTestId: "app-develop-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/develop(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-develop-page");

    await navigateBySidebar(page, "library", {
      pageTestId: "app-library-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/library(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-library-page");

    await navigateBySidebar(page, "agent-chat", {
      pageTestId: "app-agent-chat-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/chat(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-agent-chat-page");

    await navigateBySidebar(page, "ai-assistant", {
      pageTestId: "app-ai-assistant-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/assistant(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-ai-assistant-page");

    await navigateBySidebar(page, "model-configs", {
      pageTestId: "app-model-configs-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/model-configs(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-model-configs-page");

    await navigateBySidebar(page, "workflows", {
      pageTestId: "app-workflows-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/work_flow(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-workflows-page");

    await navigateBySidebar(page, "users", {
      pageTestId: "app-users-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/users(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-users-page");

    await navigateBySidebar(page, "roles", {
      pageTestId: "app-roles-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/roles(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-roles-page");

    await navigateBySidebar(page, "departments", {
      pageTestId: "app-departments-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/departments(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-departments-page");

    await navigateBySidebar(page, "positions", {
      pageTestId: "app-positions-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/positions(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-positions-page");

    await navigateBySidebar(page, "approval", {
      pageTestId: "app-approval-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/approval(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-approval-page");

    await navigateBySidebar(page, "reports", {
      pageTestId: "app-reports-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/reports(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-reports-page");

    await navigateBySidebar(page, "dashboards", {
      pageTestId: "app-dashboards-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/dashboards(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-dashboards-page");

    await navigateBySidebar(page, "visualization", {
      pageTestId: "app-visualization-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/visualization(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-visualization-page");

    await navigateBySidebar(page, "settings", {
      pageTestId: "app-settings-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/settings(?:\\?.*)?$`)
    });
    await expectNoI18nKeyLeak(page, "app-settings-page");
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
    await expect(page.getByTestId("app-sidebar-item-develop")).toContainText("开发台");
    await expect(page.getByTestId("app-sidebar-item-library")).toContainText("资源库");
    await expect(page.getByTestId("app-sidebar-item-agent-chat")).toContainText("Agent 对话");
    await expect(page.getByTestId("app-sidebar-item-ai-assistant")).toContainText("AI 助手");
    await expect(page.getByTestId("app-sidebar-item-model-configs")).toContainText("模型配置");
    await expect(page.getByTestId("app-sidebar-item-workflows")).toContainText("工作流");

    await page.getByTestId("app-primary-item-admin").click();
    await expect(page.getByTestId("app-sidebar-item-users")).toContainText("用户");
    await expect(page.getByTestId("app-sidebar-item-roles")).toContainText("角色");
    await expect(page.getByTestId("app-sidebar-item-departments")).toContainText("部门");
    await expect(page.getByTestId("app-sidebar-item-positions")).toContainText("职位");
    await expect(page.getByTestId("app-sidebar-item-approval")).toContainText("审批工作台");
    await expect(page.getByTestId("app-sidebar-item-reports")).toContainText("报表管理");
    await expect(page.getByTestId("app-sidebar-item-dashboards")).toContainText("仪表盘管理");
    await expect(page.getByTestId("app-sidebar-item-visualization")).toContainText("运行监控");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("设置");

    await page.getByTestId("app-primary-item-explore").click();
    await expect(page.getByTestId("app-sidebar-item-explore-plugins")).toContainText("插件商店");
    await expect(page.getByTestId("app-sidebar-item-explore-templates")).toContainText("模板商店");
  });
});
