import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup
} from "./helpers";

test.describe.serial("App Settings And Maintenance", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    // 当前 sidebar "settings" 指向工作空间设置（成员/发布渠道）；
    // 应用管理-数据库设置仍在 legacy 路径 /apps/<appKey>/admin/settings
    // 这里直接 goto 该 legacy 路径，由 legacy-route-mapping 统一处理。
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/admin/settings`);
    await expect(page.getByTestId("app-settings-page")).toBeVisible({ timeout: 30_000 });
  });

  test("database tab should test connection and trigger backup", async ({ page }) => {
    await expect(page.getByTestId("app-settings-db-tab")).toBeVisible();

    await page.getByTestId("app-settings-db-test-connection").click();
    await expect(page.getByTestId("app-settings-db-connection-result")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("app-settings-db-backup-now").click();
    await expect(page.getByTestId("app-settings-db-backup-table")).toBeVisible();
  });
});

