import { expect, test } from "@playwright/test";

test("可视化入口登录态守卫生效", async ({ page }) => {
  await page.goto("/visualization");
  await expect(page).toHaveURL(/\/login/);
});
