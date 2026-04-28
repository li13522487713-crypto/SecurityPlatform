import { expect, test } from "../fixtures/single-session";
import { selectWorkspacePath } from "@atlas/app-shell-shared";
import {
  appBaseUrl,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  navigateBySidebar,
  seedLocale
} from "./helpers";

async function openCanonicalWorkspace(page: import("@playwright/test").Page, appKey: string) {
  await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
  const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
  if (await matchedWorkspaceButton.count()) {
    await matchedWorkspaceButton.click();
  } else {
    await page.locator('[data-testid^="coze-select-workspace-"]').first().click();
  }
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("coze-home-page")).toBeVisible({ timeout: 30_000 });
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
      { itemKey: "home", pageTestId: "coze-home-page" },
      { itemKey: "projects", pageTestId: "coze-projects-page" },
      { itemKey: "resources", pageTestId: "coze-resource-page" },
      { itemKey: "tasks", pageTestId: "coze-tasks-page" },
      { itemKey: "evaluations", pageTestId: "coze-evaluations-page" },
      { itemKey: "settings", pageTestId: "coze-settings-publish-page" },
      { itemKey: "templates", pageTestId: "coze-market-templates-page" },
      { itemKey: "plugins", pageTestId: "coze-market-plugins-page" },
      { itemKey: "community", pageTestId: "coze-community-page" },
      { itemKey: "open-api", pageTestId: "coze-open-api-page" },
      { itemKey: "docs", pageTestId: "coze-docs-page" },
      { itemKey: "platform", pageTestId: "coze-platform-general-page" }
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
    await expect(page.getByTestId("coze-me-profile-page")).toBeVisible();
    await expect(page.url()).toContain("/me/profile");
    await expectNoI18nKeyLeak(page, "coze-me-profile-page");
  });

  test("workspace navigation should render the new zh-CN information architecture", async ({ page }) => {
    await seedLocale(page, "zh-CN");
    await openCanonicalWorkspace(page, appKey);

    await expect(page.getByTestId("app-sidebar-item-home")).toContainText("首页");
    await expect(page.getByTestId("app-sidebar-item-projects")).toContainText("项目开发");
    await expect(page.getByTestId("app-sidebar-item-resources")).toContainText("资源库");
    await expect(page.getByTestId("app-sidebar-item-tasks")).toContainText("任务中心");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("空间配置");

    await navigateBySidebar(page, "home", { pageTestId: "coze-home-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("首页");

    await navigateBySidebar(page, "tasks", { pageTestId: "coze-tasks-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("任务中心");

    await navigateBySidebar(page, "settings", { pageTestId: "coze-settings-publish-page" });
    await expect(page.getByTestId("app-shell-header-title")).toHaveText("空间配置");
  });
});
