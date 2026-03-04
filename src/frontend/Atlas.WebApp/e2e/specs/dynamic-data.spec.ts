import { expect, test } from "@playwright/test";

test("动态数据入口登录态守卫生效", async ({ page }) => {
  await page.goto("/dynamic-tables");
  await expect(page).toHaveURL(/\/login/);
});
