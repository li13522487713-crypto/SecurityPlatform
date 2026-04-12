import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppWorkspace,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  loginApp,
  resolveCanonicalAppKey,
  seedLocale
} from "./helpers";

test.describe.serial("@smoke App Auth And Routing", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    if (!appKey) {
      appKey = await resolveCanonicalAppKey(request);
    }
    await ensureLoggedInSession(appKey);
  });

  test("login success should enter dashboard", async ({ page }) => {
    await ensureAppWorkspace(page, appKey);
    await expectNoI18nKeyLeak(page, "app-develop-page");
  });

  test("wrong password should stay at login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await loginApp(page, appKey, "WrongPassword#123", { expectSuccess: false });
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
    await expect(page.locator(".login-error")).toBeVisible();
  });

  test("unauthenticated workspace should redirect login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
  });

  test("invalid app route should normalize to canonical appKey without refresh", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/456/space/atlas-space/develop`);
    await expect(page).toHaveURL(new RegExp(`/apps/${appKey}/login`));
  });

  test("en-US locale should persist after reload", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await seedLocale(page, "en-US");
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/login`);
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
    await page.reload();
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
  });
});

