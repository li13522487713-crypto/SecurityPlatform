import { expect, test } from "@playwright/test";

test("资产入口登录态守卫生效", async ({ page }) => {
  await page.goto("/assets");
  await expect(page).toHaveURL(/\/login/);
});
