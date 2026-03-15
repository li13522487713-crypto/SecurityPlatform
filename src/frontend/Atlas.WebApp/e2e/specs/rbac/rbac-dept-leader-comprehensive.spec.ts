import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC 综合测试 - 部门领导 (DeptAdminA) 角色", () => {
  // NOTE: We use DeptAdminA assuming it's correctly mapped in DB seeds to have "部门领导" permissions.
  // Testing across 4 dimensions: Menu Display, Route Guard, Data Scope, Operation Filtering.

  test("1. 菜单权限: 可见与不可见菜单验证", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();

    // -- 期望可见菜单 (根据实际产品可能存在的菜单部分调整) --
    // 本部门主页 / 工作流中心 (审批管理) / 部门成员(系统管理中的部分能力)
    await expect(page.getByRole('menuitem', { name: '控制台' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: '组织架构' }).or(page.getByRole('menuitem', { name: '工作流引擎' }))).toBeVisible();
    
    // -- 期望不可见菜单 (系统底层管理) --
    await expect(page.getByRole('menuitem', { name: '安全中心' })).not.toBeVisible();
    await expect(page.getByRole('menuitem', { name: '系统配置' })).not.toBeVisible();
    await expect(page.getByRole('menuitem', { name: '权限管理' })).not.toBeVisible();
  });

  test("2. 页面拦截: 即使通过链接直达，也不能访问无权限内容", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();

    // 尝试直接访问受限的各种 URL
    const restrictedUrls = [
      '/settings/auth/roles',      // 角色配置
      '/settings/system/configs',  // 系统配置
      '/monitor/message-queue'     // 跨部门/系统运维
    ];

    for (const url of restrictedUrls) {
      await page.goto(url);
      await page.waitForTimeout(500); // Wait for redirect
      const currentUrl = page.url();
      // Should intercept and redirect to home or 403
      expect(currentUrl).not.toContain(url);
    }
  });

  test("3. 数据权限: 只能看到本部门/授权部门数据", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();

    // Navigate to user management which is often where data boundaries are most obvious
    await page.goto("/settings/org/users");

    // 期望能看到自己部门的成员 (如 user.a.e2e 属于研发部一组)
    await page.getByPlaceholder("搜索...").fill("user.a.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    await page.waitForTimeout(500);
    await expect(page.locator("tr", { hasText: "user.a.e2e" })).toBeVisible();

    // 不期望看到其他部门成员 (如 user.b.e2e 属于安全运营部)
    await page.getByPlaceholder("搜索...").fill("user.b.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    await page.waitForTimeout(500);
    await expect(page.locator("tr", { hasText: "user.b.e2e" })).not.toBeVisible();
  });

  test("4. 按钮与操作权限: 无系统全局删除、配置等危险操作", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();

    await page.goto("/settings/org/users");

    // "新增员工"、"批量删除"等全局管理级别的按钮，对于普通部门领导在没有额外赋权时应该不可见。
    const btnAdd = page.getByRole("button", { name: "新增员工" });
    const btnDelete = page.getByRole("button", { name: "批量删除" });

    // Ensure buttons are hidden or disabled due to role
    if (await btnAdd.count() > 0) {
      await expect(btnAdd).toBeDisabled();
    } else {
      await expect(btnAdd).not.toBeVisible();
    }
    
    if (await btnDelete.count() > 0) {
      await expect(btnDelete).toBeDisabled();
    } else {
       await expect(btnDelete).not.toBeVisible();
    }

    // Attempting to hit an API directly for a restricted resource would require 
    // extracting the bearer token from sessionStorage. For this pure GUI test, 
    // ensuring the UI elements are hidden/disabled is sufficient.
  });
});
