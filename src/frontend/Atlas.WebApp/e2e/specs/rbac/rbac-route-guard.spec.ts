import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 路由拦截", () => {
  // RBAC-ROUTE-001: readonly straight access `/settings/auth/roles` blocked/redirected
  test("RBAC-ROUTE-001 只读账号直访角色管理被拦截", async ({ page, loginAsReadonly }) => {
    await loginAsReadonly();

    // Attempt direct access
    await page.goto("/settings/auth/roles");

    // Wait for network idle or potential redirect. Our app either redirects to 403 / 404 or back to /console.
    // Let's assert that the URL changes away from /settings/auth/roles or a visible forbidden message.
    await page.waitForTimeout(1000); // give it a beat to redirect

    const currentUrl = page.url();
    expect(currentUrl).not.toContain("/settings/auth/roles");
    
    // Check if error message is shown (if not redirected)
    // await expect(page.getByText('403').or(page.getByText('无权限'))).toBeVisible(); 
  });

  // RBAC-ROUTE-006: user.a straight access `/settings/org/users` blocked
  test("RBAC-ROUTE-006 普通用户直访用户管理被拦截", async ({ page, loginAsUserA }) => {
    await loginAsUserA();

    // Attempt direct access
    await page.goto("/settings/org/users");

    await page.waitForTimeout(1000); // wait for redirect to kick in

    const currentUrl = page.url();
    expect(currentUrl).not.toContain("/settings/org/users");
  });
});
