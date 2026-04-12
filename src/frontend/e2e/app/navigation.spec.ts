import { expect, test, type Page } from "../fixtures/single-session";
import {
  ensureAppSetup,
  expectNoI18nKeyLeak,
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
    await page.goto(`/apps/${encodeURIComponent(appKey)}/dashboard`);
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
});

