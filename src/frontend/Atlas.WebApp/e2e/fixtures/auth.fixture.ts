import { test as base, expect } from "@playwright/test";

type AuthFixture = {
  loginAsAdmin: () => Promise<void>;
};

export const test = base.extend<AuthFixture>({
  loginAsAdmin: async ({ page }, use) => {
    await use(async () => {
      await page.goto("/login");
      await page.getByPlaceholder("请输入租户 / 组织 ID").fill("00000000-0000-0000-0000-000000000001");
      await page.getByPlaceholder("请输入手机号/邮箱/用户名").fill("admin");
      await page.getByPlaceholder("请输入密码").fill("P@ssw0rd!");
      await page.getByRole("button", { name: "登录" }).click();
      await page.waitForURL(/\/(home|$)/, { timeout: 15000 });
    });
  }
});

export { expect };
