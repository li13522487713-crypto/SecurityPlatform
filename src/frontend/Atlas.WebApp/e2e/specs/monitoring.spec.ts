import { expect, test } from "@playwright/test";

test("监控审计入口登录态守卫生效", async ({ page }) => {
  await page.goto("/monitor");
  await expect(page).toHaveURL(/\/login/);
});
