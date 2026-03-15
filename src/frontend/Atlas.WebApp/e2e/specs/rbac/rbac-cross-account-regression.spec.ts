import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 跨账号与状态回归", () => {
  // We need to modify permissions then verify. We'll do it sequentially in one test to avoid 
  // polluting the environment too much.
  
  // RBAC-REG-002: role permissions adjusted, immediately takes effect after re-login
  test("RBAC-REG-002 角色权限调整后，重新登录立即生效", async ({ browser }) => {
    // 1. Log in as SuperAdmin to give ReadOnlyAuditor to user.b (who currently does not have it)
    const context1 = await browser.newContext();
    const page1 = await context1.newPage();
    const tenantId = process.env.E2E_TEST_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
    
    await page1.goto("/login");
    await page1.getByPlaceholder("请输入租户").fill(tenantId);
    await page1.getByPlaceholder("用户名").fill("superadmin.e2e");
    await page1.getByPlaceholder("密码").fill(process.env.E2E_TEST_PASSWORD || "P@ssw0rd!");
    await page1.getByRole("button", { name: "登录" }).click();
    
    // Attempt to assign role
    await page1.goto("/settings/org/users");
    await page1.getByPlaceholder("搜索...").fill("user.b.e2e");
    await page1.getByPlaceholder("搜索...").press("Enter");
    await page1.waitForTimeout(500);

    const row = page1.locator("tr").filter({ hasText: "user.b.e2e" });
    await row.getByRole("button", { name: "配置", exact: false }).click();
    await page1.getByLabel("角色").click();
    await page1.getByText("ReadOnlyAuditor", { exact: false }).click();
    await page1.getByRole("button", { name: "确 定" }).click();
    
    await expect(page1.getByText("更新成功", { exact: false })).toBeVisible();
    await context1.close();

    // 2. Log in as user.b and see if the new menu appears
    const context2 = await browser.newContext();
    const page2 = await context2.newPage();
    await page2.goto("/login");
    await page2.getByPlaceholder("请输入租户").fill(tenantId);
    await page2.getByPlaceholder("用户名").fill("user.b.e2e");
    await page2.getByPlaceholder("密码").fill(process.env.E2E_TEST_PASSWORD || "P@ssw0rd!");
    await page2.getByRole("button", { name: "登录" }).click();
    
    // Since they now have ReadOnlyAuditor, they should see "审计日志"
    await expect(page2.getByText('审计日志')).toBeVisible();
    await context2.close();

    // 3. Clean up (remove role)
    const context3 = await browser.newContext();
    const page3 = await context3.newPage();
    await page3.goto("/login");
    await page3.getByPlaceholder("请输入租户").fill(tenantId);
    await page3.getByPlaceholder("用户名").fill("superadmin.e2e");
    await page3.getByPlaceholder("密码").fill(process.env.E2E_TEST_PASSWORD || "P@ssw0rd!");
    await page3.getByRole("button", { name: "登录" }).click();
    
    await page3.goto("/settings/org/users");
    await page3.getByPlaceholder("搜索...").fill("user.b.e2e");
    await page3.getByPlaceholder("搜索...").press("Enter");
    await page3.waitForTimeout(500);

    const rowClean = page3.locator("tr").filter({ hasText: "user.b.e2e" });
    await rowClean.getByRole("button", { name: "配置", exact: false }).click();
    await page3.getByLabel("角色").click();
    await page3.getByText("ReadOnlyAuditor", { exact: false }).click(); // toggle off
    await page3.getByRole("button", { name: "确 定" }).click();
    await context3.close();
  });
});
