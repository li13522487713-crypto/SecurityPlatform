import { expect, test } from "@playwright/test";

test("审批与工作流入口可用", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByText("统一安全管理 & 运维管控")).toBeVisible();
});
