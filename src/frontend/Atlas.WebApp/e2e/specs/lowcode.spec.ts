import { expect, test } from "@playwright/test";

test("低代码入口登录页元素存在", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByText("智控 · 守护 · 可审计")).toBeVisible();
});
