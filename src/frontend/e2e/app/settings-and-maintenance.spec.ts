import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup
} from "./helpers";

test.describe.serial("App Settings And Maintenance", () => {
  test.fixme("旧壳应用设置/数据库维护入口已下线，待新壳对应设置场景补齐后恢复。");
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    // 该用例组已 fixme；保留最小初始化逻辑，避免后续恢复时丢失上下文。
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

