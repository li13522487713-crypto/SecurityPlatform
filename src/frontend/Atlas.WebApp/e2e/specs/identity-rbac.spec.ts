import { expect, test } from "@playwright/test";

test("身份权限链路入口可渲染", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByPlaceholder("请输入手机号/邮箱/用户名")).toBeVisible();
  await expect(page.getByPlaceholder("请输入密码")).toBeVisible();
});
