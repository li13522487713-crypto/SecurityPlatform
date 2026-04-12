import { expect, test } from "../fixtures/single-session";
import {
  ensureAppSetup
} from "./helpers";

test.describe.serial("App Settings And Maintenance", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`/apps/${encodeURIComponent(appKey)}/settings`);
    await expect(page.getByTestId("app-settings-page")).toBeVisible();
  });

  test("database tab should test connection and trigger backup", async ({ page }) => {
    await page.getByRole("tab", { name: /数据库运维|Database/ }).click();
    await expect(page.getByTestId("app-settings-db-tab")).toBeVisible();

    await page.getByTestId("app-settings-db-test-connection").click();
    await expect(page.getByTestId("app-settings-db-connection-result")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("app-settings-db-backup-now").click();
    await expect(page.getByTestId("app-settings-db-backup-table")).toBeVisible();
  });
});

