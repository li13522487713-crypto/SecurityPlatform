import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 角色菜单与功能权限分配", () => {
  // NOTE: Tests run in parallel generally. In E2E, it's often better to avoid tests that depend on 
  // exact sequential order unless they run in serial mode.
  // We will configure this file or block to run in serial.
  test.describe.configure({ mode: 'serial' });

  // RBAC-ROLE-004: superadmin assigns ReadOnlyAuditor to user.a
  test("RBAC-ROLE-004 超级管理员为普通用户A分配只读审计角色", async ({ page, loginAsSuperAdmin }) => {
    await loginAsSuperAdmin();

    await page.goto("/settings/org/users");

    // Search user a
    await page.getByPlaceholder("搜索...").fill("user.a.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    
    // Wait for table to load search results
    await page.waitForTimeout(500);

    // Assuming we click the "Authorize" or "Assign Role" button from actions
    const row = page.locator("tr").filter({ hasText: "user.a.e2e" });
    await row.getByRole("button", { name: "配置", exact: false }).click();

    // The UI should pop up a drawer/modal to edit the user's roles
    // Select the ReadOnlyAuditor role
    // NOTE: If using Ant Design Select, it might involve opening a dropdown
    await page.getByLabel("角色").click();
    await page.getByText("ReadOnlyAuditor", { exact: false }).click();
    
    // Save
    await page.getByRole("button", { name: "确 定" }).click();
    
    await expect(page.getByText("更新成功", { exact: false })).toBeVisible();
  });

  // RBAC-ROLE-005: user.a logged in, sees ReadOnlyAuditor menus
  test("RBAC-ROLE-005 重新登录后，验证权限", async ({ page, loginAsUserA }) => {
    // Relies on previous step completing successfully
    await loginAsUserA();

    // Because user.a now ALSO has ReadOnlyAuditor roles, they should see audit logs
    await expect(page.getByText('审计日志')).toBeVisible();
    await expect(page.getByText('登录日志')).toBeVisible();
  });

  // At the end, clean up (Restore user.a back to normal role if possible)
  test("Clean up: 还原普通用户A角色", async ({ page, loginAsSuperAdmin }) => {
    await loginAsSuperAdmin();
    await page.goto("/settings/org/users");
    await page.getByPlaceholder("搜索...").fill("user.a.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    await page.waitForTimeout(500);
    
    // Remove ReadOnlyAuditor
    const row = page.locator("tr").filter({ hasText: "user.a.e2e" });
    await row.getByRole("button", { name: "配置", exact: false }).click();
    await page.getByLabel("角色").click();
    await page.getByText("ReadOnlyAuditor", { exact: false }).click(); // Toggle it off
    await page.getByRole("button", { name: "确 定" }).click();
  });

  // RBAC-ROLE-009: deptadmin.a opens user mgmt, add/del buttons hidden
  test("RBAC-ROLE-009 部门管理员A仅有查询权限，无写操作按钮", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();
    await page.goto("/settings/org/users");

    // They should be able to see the page but not write
    await expect(page.getByRole("button", { name: "新增员工" })).not.toBeVisible();
    await expect(page.getByRole("button", { name: "批量删除" })).not.toBeVisible();
  });
});
