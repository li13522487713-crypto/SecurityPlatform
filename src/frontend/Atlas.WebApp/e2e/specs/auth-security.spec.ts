import { expect, test } from "@playwright/test";

test("认证安全页面可访问登录入口", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByRole("button", { name: "登录" })).toBeVisible();
  await expect(page.getByText("欢迎登录")).toBeVisible();
});
