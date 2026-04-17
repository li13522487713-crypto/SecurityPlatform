import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, expectNoI18nKeyLeak, seedLocale } from "./helpers";

/**
 * E2E：/setup-console 二次认证门（M2）。
 *
 * 控制台永久免登录，但必须用恢复密钥或 BootstrapAdmin 凭证完成二次认证才能进入。
 * 两种凭证默认 mock 见 `services/mock/api-setup-console.mock.ts`：
 *   - recoveryKey = "ATLS-MOCK-AAAA-BBBB-CCCC-DDDD"
 *   - bootstrapAdmin = "admin" / "P@ssw0rd!"
 *
 * 本 spec 不依赖任何后端能力，仅验证前端 mock 流程。
 */
test.describe.serial("Setup Console - Auth Gate", () => {
  test("first visit lands on the auth gate (no console session yet)", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await seedLocale(page, "zh-CN");

    await page.goto(`${appBaseUrl}/setup-console`);

    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
    await expectNoI18nKeyLeak(page, "setup-console-auth-gate");
  });

  test("invalid recovery key shows inline error and stays on auth gate", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-recovery-key").fill("WRONG-KEY-DOES-NOT-EXIST");
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-auth-error")).toBeVisible();
    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
  });

  test("valid recovery key unlocks the console and lands on dashboard", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page
      .getByTestId("setup-console-auth-recovery-key")
      .fill("ATLS-MOCK-AAAA-BBBB-CCCC-DDDD");
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-page")).toBeVisible();
    await expect(page.getByTestId("setup-console-tab-bar")).toBeVisible();
    await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
  });

  test("bootstrap admin credentials also unlock the console", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page.getByTestId("setup-console-auth-bootstrap-username").fill("admin");
    await page.getByTestId("setup-console-auth-bootstrap-password").fill("P@ssw0rd!");
    await page.getByTestId("setup-console-auth-submit").click();

    await expect(page.getByTestId("setup-console-page")).toBeVisible();
    await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
  });

  test("logout clears console session and shows auth gate again", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await page.goto(`${appBaseUrl}/setup-console`);

    await page
      .getByTestId("setup-console-auth-recovery-key")
      .fill("ATLS-MOCK-AAAA-BBBB-CCCC-DDDD");
    await page.getByTestId("setup-console-auth-submit").click();
    await expect(page.getByTestId("setup-console-page")).toBeVisible();

    await page.getByTestId("setup-console-logout").click();
    await expect(page.getByTestId("setup-console-auth-gate")).toBeVisible();
    await expect(page.getByTestId("setup-console-page")).toHaveCount(0);
  });
});
