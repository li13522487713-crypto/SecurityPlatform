import { expect, test, type Page } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  seedLocale,
} from "./helpers";

async function clickSidebarAndAssertPage(
  page: Page,
  sidebarItemTestId: string,
  routeSuffix: string,
  pageRootTestId: string
) {
  await page.getByTestId(sidebarItemTestId).click();
  await page.waitForURL(new RegExp(`/apps/[^/]+/${routeSuffix}(?:\\?.*)?$`), { timeout: 20_000 });
  await expect(page.getByTestId(pageRootTestId)).toBeVisible({ timeout: 20_000 });
  await expectNoI18nKeyLeak(page, pageRootTestId);
}

test.describe.serial("@smoke App Navigation", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);
    await expect(page.getByTestId("app-sidebar")).toBeVisible();
  });

  test("sidebar should navigate to real pages", async ({ page }) => {
    await clickSidebarAndAssertPage(page, "app-sidebar-item-dashboard", "dashboard", "app-dashboard-page");
    await clickSidebarAndAssertPage(page, "app-sidebar-item-users", "users", "app-users-page");
    await clickSidebarAndAssertPage(page, "app-sidebar-item-roles", "roles", "app-roles-page");
    await clickSidebarAndAssertPage(page, "app-sidebar-item-departments", "departments", "app-departments-page");
    await clickSidebarAndAssertPage(page, "app-sidebar-item-positions", "positions", "app-positions-page");
    await clickSidebarAndAssertPage(page, "app-sidebar-item-settings", "settings", "app-settings-page");
  });

  test("header user menu should open and route profile", async ({ page }) => {
    await page.getByTestId("app-header-user-menu").click();
    await page.getByTestId("app-header-menu-profile").click();
    await expect(page.getByTestId("app-profile-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "app-profile-page");
  });

  test("app organization navigation should render in zh-CN", async ({ page }) => {
    await seedLocale(page, "zh-CN");
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);
    await expect(page.getByTestId("app-sidebar-item-dashboard")).toContainText("概览");
    await expect(page.getByTestId("app-sidebar-item-users")).toContainText("用户");
    await expect(page.getByTestId("app-sidebar-item-roles")).toContainText("角色");
    await expect(page.getByTestId("app-sidebar-item-departments")).toContainText("部门");
    await expect(page.getByTestId("app-sidebar-item-positions")).toContainText("职位");
    await expect(page.getByTestId("app-sidebar-item-settings")).toContainText("设置");

    await page.getByTestId("app-sidebar-item-departments").click();
    await expect(page.getByTestId("app-departments-page")).toContainText("部门管理");
  });
});

