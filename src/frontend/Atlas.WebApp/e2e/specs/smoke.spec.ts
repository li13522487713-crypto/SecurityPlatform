import { expect, test } from "@playwright/test";

test("登录页基础渲染", async ({ page }) => {
  await page.goto("/login");

  await expect(page.getByRole("heading", { name: "安全控制台" })).toBeVisible();
  await expect(page.getByRole("button", { name: "登录" })).toBeVisible();
  await expect(page.getByText("统一安全管理 & 运维管控")).toBeVisible();
});
