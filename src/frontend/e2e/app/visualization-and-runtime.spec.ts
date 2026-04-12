import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup
} from "./helpers";

test.describe.serial("App Visualization And Runtime", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("visualization page should load", async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/visualization`);
    await expect(page.getByTestId("app-visualization-page")).toBeVisible();
    await expect(page.getByTestId("app-visualization-table")).toBeVisible();
  });

  test("entry page should resolve to runtime or show deterministic fallback", async ({ page }) => {
    test.setTimeout(90_000);
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/entry`);
    await expect.poll(
      async () => {
        const currentUrl = page.url();
        if (/\/apps\/[^/]+\/r\/[^/]+/.test(currentUrl)) {
          return "runtime";
        }

        const warningResult = await page.getByTestId("app-entry-gateway-warning").isVisible().catch(() => false);
        if (warningResult) {
          return "fallback";
        }

        return "pending";
      },
      { timeout: 60_000 }
    ).not.toBe("pending");

    if (/\/apps\/[^/]+\/r\/[^/]+/.test(page.url())) {
      await expect(page.getByTestId("app-runtime-page")).toBeVisible();
    } else {
      await expect(page.getByTestId("app-entry-gateway-warning")).toBeVisible();
    }
  });
});

