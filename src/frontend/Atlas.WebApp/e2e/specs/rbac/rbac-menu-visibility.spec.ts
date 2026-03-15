import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 菜单可见性", () => {
  // RBAC-MENU-001: superadmin sees all menus
  test("RBAC-MENU-001 超级管理员可见全部菜单", async ({ page, loginAsSuperAdmin }) => {
    await loginAsSuperAdmin();

    // Verify all top-level menus are visible
    await expect(page.getByRole('menuitem', { name: '控制台' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '组织架构' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '权限管理' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '系统配置' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '安全中心' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '运维监控' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '工作流引擎' })).toBeVisible();
  });

  // RBAC-MENU-004: securityadmin cannot see system settings
  test("RBAC-MENU-004 安全管理员不可见系统配置菜单", async ({ page, loginAsSecurityAdmin }) => {
    await loginAsSecurityAdmin();

    // Should see security center
    await expect(page.getByRole('menuitem', { name: '安全中心' })).toBeVisible();

    // Should NOT see system config, workflow engine, permissions
    await expect(page.getByRole('menuitem', { name: '系统配置' })).not.toBeVisible();
    await expect(page.getByRole('menuitem', { name: '工作流引擎' })).not.toBeVisible();
    await expect(page.getByRole('menuitem', { name: '权限管理' })).not.toBeVisible();
  });

  // RBAC-MENU-006: readonly sees audit/login/notice menus
  test("RBAC-MENU-006 只读审计账号仅可见审计日志/登录日志/通知中心", async ({ page, loginAsReadonly }) => {
    await loginAsReadonly();

    // Verify specific sub-menus are visible under System Settings / Security Center or wherever they are located.
    // Assuming they are under "System Settings" or "Security Center". We'll verify the exact text items.
    // In our app, typically clicking root menus expands the sub-menus in the sidebar.
    // This provides a resilient way to check if the specific leaf menus are visible somewhere on the page.
    await expect(page.getByText('审计日志')).toBeVisible();
    await expect(page.getByText('登录日志')).toBeVisible();
    await expect(page.getByText('通知中心')).toBeVisible();

    // Verify they CANNOT see other sensitive things
    await expect(page.getByText('系统设置')).not.toBeVisible();
    await expect(page.getByText('权限管理')).not.toBeVisible();
    await expect(page.getByText('用户管理')).not.toBeVisible();
  });
});
