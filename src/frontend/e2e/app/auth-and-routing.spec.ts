import { expect, test } from "../fixtures/single-session";
import { orgWorkspacesPath, signPath } from "@atlas/app-shell-shared";
import {
  appBaseUrl,
  defaultTenantId,
  ensureAppSetup,
  ensureAppWorkspace,
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

  test("login success should enter workspace list", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await loginApp(page, appKey);
    await expect(page).toHaveURL(new RegExp(`${orgWorkspacesPath(defaultTenantId)}(?:\\?.*)?$`));
    await expect(page.getByTestId("workspace-list-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "workspace-list-page");
  });

  test("authenticated session should enter workspace dashboard", async ({ page }) => {
    await ensureAppWorkspace(page, appKey);
    await expect(page.getByTestId("app-dashboard-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "app-dashboard-page");
  });

  test("wrong password should stay at canonical login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await loginApp(page, appKey, "WrongPassword#123", { expectSuccess: false });
    await expect(page).toHaveURL(new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`));
    await expect(page.locator(".login-error")).toBeVisible();
  });

  test("unauthenticated workspace list should redirect login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}${orgWorkspacesPath(defaultTenantId)}`);
    await expect(page).toHaveURL(new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`));
  });

  test("legacy app route should redirect to canonical login when unauthenticated", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
    await expect(page).toHaveURL(new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`));
  });

  test("en-US locale should persist after reload", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await seedLocale(page, "en-US");
    await page.goto(`${appBaseUrl}${signPath()}`);
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
    await page.reload();
    await expect(page.getByTestId("app-login-submit")).toContainText("Login");
    await expectNoI18nKeyLeak(page, "app-login-page");
  });
});
