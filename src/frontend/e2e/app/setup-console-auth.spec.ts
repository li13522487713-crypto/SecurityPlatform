import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, captureEvidenceScreenshot, expectNoI18nKeyLeak, seedLocale } from "./helpers";

/**
 * E2E：/setup-console 二次认证门（M2）。
 *
 * 控制台永久免登录，但必须用恢复密钥或 BootstrapAdmin 凭证完成二次认证才能进入。
 * 本 spec 连接真实后端 setup-console API；恢复密钥用 SETUP_CONSOLE_RECOVERY_KEY 注入。
 */
test.describe.serial("Setup Console - Auth Gate", () => {
  test("first visit lands on the auth gate (no console session yet)", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await seedLocale(page, "zh-CN");

    await page.goto(`${appBaseUrl}/setup-console`);

    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
    await expectNoI18nKeyLeak(page, "setup-console-auth-gate");
    await captureEvidenceScreenshot(page, testInfo, "setup-console-auth-gate");
  });

  test("invalid recovery key shows inline error and stays on auth gate", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-recovery-key").fill("WRONG-KEY-DOES-NOT-EXIST");
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-auth-error")).toBeVisible();
    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
    await captureEvidenceScreenshot(page, testInfo, "setup-console-auth-invalid-key");
  });

  test("valid recovery key unlocks the console and lands on dashboard", async ({ page, resetAuthForCase }, testInfo) => {
    const recoveryKey = process.env.SETUP_CONSOLE_RECOVERY_KEY?.trim();
    test.skip(!recoveryKey, "SETUP_CONSOLE_RECOVERY_KEY is required for recovery-key auth coverage.");

    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-recovery-key").fill(recoveryKey);
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-page")).toBeVisible();
    await expect(page.getByTestId("setup-console-tab-bar")).toBeVisible();
    await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-auth-recovery-success");
  });

  test("bootstrap admin credentials also unlock the console", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-bootstrap-username").fill("admin");
    await page.getByTestId("setup-console-auth-bootstrap-password").fill("P@ssw0rd!");
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-page")).toBeVisible();
    await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-auth-bootstrap-success");
  });

  test("logout clears console session and shows auth gate again", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-bootstrap-username").fill("admin");
    await page.getByTestId("setup-console-auth-bootstrap-password").fill("P@ssw0rd!");
    await page.getByTestId("setup-console-auth-submit").click();
    await expect(page.getByTestId("setup-console-page")).toBeVisible();

    await page.getByTestId("setup-console-logout").click();
    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
    await captureEvidenceScreenshot(page, testInfo, "setup-console-auth-logout");
  });
});
