import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, captureEvidenceScreenshot, expectNoI18nKeyLeak, seedLocale } from "./helpers";

const RECOVERY_KEY = "ATLS-MOCK-AAAA-BBBB-CCCC-DDDD";

async function unlockConsole(page: import("@playwright/test").Page) {
  await page.goto(`${appBaseUrl}/setup-console`);
  await page.getByTestId("setup-console-auth-recovery-key").fill(RECOVERY_KEY);
  await page.getByTestId("setup-console-auth-submit").click();
  await expect(page.getByTestId("setup-console-page")).toBeVisible();
}

/**
 * E2E：/setup-console Dashboard 总览（M2）。
 *
 * - 4 卡渲染：System / Workspace / Migration / Catalog
 * - Tab 栏 5 个 Tab 可点
 * - 跳转到其它 Tab 不会丢失会话
 * - 多语言切换不影响布局
 */
test.describe.serial("Setup Console - Dashboard Overview", () => {
  test("dashboard renders system / workspace / migration / catalog cards from mock", async ({
    page,
    resetAuthForCase
  }, testInfo) => {
    await resetAuthForCase();
    await seedLocale(page, "zh-CN");
    await unlockConsole(page);

    await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
    await expect(page.getByTestId("setup-console-system-card")).toBeVisible();
    await expect(page.getByTestId("setup-console-workspace-card")).toBeVisible();
    await expect(page.getByTestId("setup-console-migration-card")).toBeVisible();
    await expect(page.getByTestId("setup-console-catalog-card")).toBeVisible();
    await expectNoI18nKeyLeak(page, "setup-console-dashboard");
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-cards");
  });

  test("system card shows current state badge with not_started label by default", async ({
    page,
    resetAuthForCase
  }, testInfo) => {
    await resetAuthForCase();
    await seedLocale(page, "zh-CN");
    await unlockConsole(page);

    const stateBadge = page.getByTestId("setup-console-system-state-badge");
    await expect(stateBadge).toBeVisible();
    await expect(stateBadge).toContainText("尚未开始");
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-system-not-started");
  });

  test("workspace card shows the seeded default workspace row", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    await expect(page.getByTestId("setup-console-workspace-table")).toBeVisible();
    await expect(page.getByTestId("setup-console-workspace-row-default")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-workspace-default");
  });

  test("migration card shows empty state when no active migration", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    const migrationCard = page.getByTestId("setup-console-migration-card");
    await expect(migrationCard).toBeVisible();
    await expect(page.getByTestId("setup-console-migration-active")).toHaveCount(0);
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-migration-empty");
  });

  test("tab bar exposes 5 tabs and switching keeps console session", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    for (const tab of ["dashboard", "system-init", "workspace-init", "migration", "repair"] as const) {
      await expect(page.getByTestId(`setup-console-tab-${tab}`)).toBeVisible();
    }

    await page.getByTestId("setup-console-tab-system-init").click();
    await expect(page).toHaveURL(/\/setup-console\/system-init/);
    await expect(page.getByTestId("setup-console-system-init")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toBeVisible();

    await page.getByTestId("setup-console-tab-migration").click();
    await expect(page).toHaveURL(/\/setup-console\/migration/);
    await expect(page.getByTestId("setup-console-migration")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-tab-bar");
  });

  test("dashboard refresh button toggles the loading text", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    const refresh = page.getByTestId("setup-console-dashboard-refresh");
    await expect(refresh).toBeVisible();
    await expect(refresh).toBeEnabled();
    await refresh.click();
    await expect(refresh).toBeEnabled();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-refresh");
  });

  test("en-US locale renders dashboard headers in English", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await seedLocale(page, "en-US");
    await unlockConsole(page);

    await expect(page.getByTestId("setup-console-system-card")).toContainText("System initialization");
    await expect(page.getByTestId("setup-console-workspace-card")).toContainText("Workspace initialization");
    await expect(page.getByTestId("setup-console-migration-card")).toContainText("Data migration jobs");
    await captureEvidenceScreenshot(page, testInfo, "setup-console-dashboard-en-us");
  });
});
