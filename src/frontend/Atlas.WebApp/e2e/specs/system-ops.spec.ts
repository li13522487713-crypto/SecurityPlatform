import { expect, test } from "@playwright/test";

test("系统运维入口登录态守卫生效", async ({ page }) => {
  await page.goto("/system-configs");
  await expect(page).toHaveURL(/\/login/);
});
