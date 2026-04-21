import { expect, test } from "../fixtures/single-session";
import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import {
  appBaseUrl,
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

  test("login success should enter workspace selector or workspace home", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await loginApp(page, appKey);
    const workspaceSelectPattern = new RegExp(`${selectWorkspacePath()}(?:\\?.*)?$`);
    const workspaceHomePattern = /\/workspace\/[^/]+\/home(?:\?.*)?$/;
    await expect(page).toHaveURL((url) =>
      workspaceSelectPattern.test(url.pathname + url.search) || workspaceHomePattern.test(url.pathname + url.search)
    );

    if (workspaceSelectPattern.test(new URL(page.url()).pathname + new URL(page.url()).search)) {
      await expect(page.getByTestId("coze-select-workspace-page")).toBeVisible();
      await expectNoI18nKeyLeak(page, "coze-select-workspace-page");
    } else {
      await expect(page.getByTestId("coze-home-page")).toBeVisible();
      await expectNoI18nKeyLeak(page, "coze-home-page");
    }
  });

  test("authenticated session should enter workspace home", async ({ page }) => {
    await ensureAppWorkspace(page, appKey);
    await expect(page.getByTestId("coze-home-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "coze-home-page");
  });

  test("wrong password should stay at canonical login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await loginApp(page, appKey, "WrongPassword#123", { expectSuccess: false });
    await expect(page).toHaveURL(new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`));
    await expect(page.locator(".login-error")).toBeVisible();
  });

  test("unauthenticated workspace selector should redirect login", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
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
