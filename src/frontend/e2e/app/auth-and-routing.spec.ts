import { expect, test } from "@playwright/test";
import {
  appBaseUrl,
  clearAuthStorage,
  ensureAppDashboard,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  loginApp,
  resolveCanonicalAppKey,
  seedLocale
} from "./helpers";

test.describe.serial("@smoke App Auth And Routing", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
    if (!appKey) {
      appKey = await resolveCanonicalAppKey(request);
    }
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
  });

  test("login success should enter dashboard", async ({ page }) => {
    await loginApp(page, appKey);
    await ensureAppDashboard(page, appKey);
    await expectNoI18nKeyLeak(page, "app-dashboard-page");
  });

  test("wrong password should stay at login", async ({ page }) => {
    await loginApp(page, appKey, "WrongPassword#123", { expectSuccess: false });
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
    await expect(page.locator(".login-error")).toBeVisible();
  });

  test("unauthenticated dashboard should redirect login", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
  });

  test("invalid app route should normalize to canonical appKey without refresh", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/456/dashboard`);
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
  });

  test("en-US locale should persist after reload", async ({ page }) => {
    await seedLocale(page, "en-US");
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/login`);
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
    await page.reload();
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
  });
});
