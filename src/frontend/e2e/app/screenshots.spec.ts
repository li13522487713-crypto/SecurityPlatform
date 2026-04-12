import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  captureEvidenceScreenshot,
  ensureAppSetup
} from "./helpers";

test.describe.serial("App Screenshot E2E", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should capture dashboard and users page screenshots in real browser", async ({ page }, testInfo) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);
    await expect(page.getByTestId("app-dashboard-page")).toBeVisible({ timeout: 20_000 });
    await captureEvidenceScreenshot(page, testInfo, "dashboard-fullpage");

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/users`);
    await expect(page.getByTestId("app-users-page")).toBeVisible({ timeout: 20_000 });
    await captureEvidenceScreenshot(page, testInfo, "users-fullpage");
  });
});

