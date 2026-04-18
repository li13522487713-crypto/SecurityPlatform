import { expect, test } from "../fixtures/single-session";
import { orgWorkspacesPath } from "@atlas/app-shell-shared";
import {
  appBaseUrl,
  defaultTenantId,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  navigateBySidebar,
  seedLocale
} from "./helpers";

async function openCanonicalWorkspace(page: import("@playwright/test").Page, appKey: string) {
  await page.goto(`${appBaseUrl}${orgWorkspacesPath(defaultTenantId)}`);
  const workspaceCard = page.locator(`.atlas-workspace-card:has-text("${appKey}")`).first();
  await expect(workspaceCard).toBeVisible({ timeout: 30_000 });
  await workspaceCard.locator('[data-testid^="workspace-open-"]').first().click();
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("app-dashboard-page")).toBeVisible({ timeout: 30_000 });
}

test.describe.serial("@smoke App Navigation", () => {
  let appKey = "";

  test.setTimeout(120_000);

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await openCanonicalWorkspace(page, appKey);
  });

  test("sidebar should navigate across canonical workspace areas", async ({ page }) => {
    const navigationCases = [
      { itemKey: "dashboard", pageTestId: "app-dashboard-page" },
      { itemKey: "develop", pageTestId: "app-develop-page" },
      { itemKey: "library", pageTestId: "app-library-page" },
      { itemKey: "manage", pageTestId: "app-organization-overview-page" },
      { itemKey: "settings", pageTestId: "workspace-settings-page" },
      { itemKey: "agent-chat", pageTestId: "app-agent-chat-page" },
      { itemKey: "model-configs", pageTestId: "app-model-configs-page" },
      { itemKey: "ai-assistant", pageTestId: "app-ai-assistant-page" },
      { itemKey: "data", pageTestId: "app-studio-data-page" },
      { itemKey: "variables", pageTestId: "app-studio-variables-page" },
      { itemKey: "users", pageTestId: "app-users-page" },
      { itemKey: "reports", pageTestId: "app-reports-page" },
      { itemKey: "dashboards", pageTestId: "app-dashboards-page" },
      { itemKey: "visualization", pageTestId: "app-visualization-page" },
      // workflows / chatflows 路由在没有具体 id 时落到 WorkspaceStudioRoute(focus=workflow|chatflow)，
      // 实际渲染 DevelopPage（testId=app-develop-page）。
      { itemKey: "workflows", pageTestId: "app-develop-page" },
      { itemKey: "chatflows", pageTestId: "app-develop-page" }
    ];

    for (const navigationCase of navigationCases) {
      await navigateBySidebar(page, navigationCase.itemKey, {
        pageTestId: navigationCase.pageTestId
      });
      await expectNoI18nKeyLeak(page, navigationCase.pageTestId);
    }
  });

  test("header user menu should route to canonical profile settings", async ({ page }) => {
    await page.getByTestId("app-header-user-menu").click();
    await page.getByTestId("app-header-menu-profile").click();
    await expect(page.getByTestId("app-profile-page")).toBeVisible();
    await expect(page.url()).toContain("/settings/profile");
    await expectNoI18nKeyLeak(page, "app-profile-page");
  });

  test("workspace navigation should render the new zh-CN information architecture", async ({ page }) => {
    await seedLocale(page, "zh-CN");
    await openCanonicalWorkspace(page, appKey);

    await expect(page.getByText("工作区导航")).toBeVisible();
    await expect(page.getByTestId("app-sidebar-item-dashboard")).toContainText("主控台");
    await expect(page.getByTestId("app-sidebar-item-develop")).toContainText("开发");
    await expect(page.getByTestId("app-sidebar-item-library")).toContainText("资源");
    await expect(page.getByTestId("app-sidebar-item-manage")).toContainText("管理");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("设置");

    await navigateBySidebar(page, "dashboard", { pageTestId: "app-dashboard-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("主控台");

    // 当前 IA 中"管理"分组的 header 文案为 i18n key shellHeaderManagementTitle = "团队治理"。
    await navigateBySidebar(page, "manage", { pageTestId: "app-organization-overview-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("团队治理");

    await navigateBySidebar(page, "settings", { pageTestId: "workspace-settings-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("设置");
  });
});
