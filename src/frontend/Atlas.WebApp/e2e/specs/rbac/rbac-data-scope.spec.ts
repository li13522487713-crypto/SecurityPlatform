import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 角色数据范围与部门权限", () => {
  // NOTE: Tests run in serial mode to maintain state
  test.describe.configure({ mode: 'serial' });

  // RBAC-SCOPE-001: superadmin sets DeptAdminA data scope to custom dept
  test("RBAC-SCOPE-001 超级管理员设置 DeptAdminA 数据范围为自定义部门", async ({ page, loginAsSuperAdmin }) => {
    await loginAsSuperAdmin();
    await page.goto("/settings/auth/roles");

    // Search and edit role
    await page.getByPlaceholder("搜索角色...").fill("DeptAdminA");
    await page.getByPlaceholder("搜索角色...").press("Enter");
    await page.waitForTimeout(500);

    const row = page.locator("tr").filter({ hasText: "DeptAdminA" });
    await row.getByRole("button", { name: "配置权限", exact: false }).click();

    await page.getByRole('tab', { name: '数据范围' }).click();
    
    // Choose custom scope
    await page.getByRole("combobox", { name: "数据范围" }).click();
    await page.getByText("自定义部门数据").click();

    // Select "研发部" and "研发一组" (if TreeSelect is used)
    await page.getByPlaceholder("请选择自定义部门").click();
    // In a real app the exact tree expansion clicks might be tricky. As a dummy click:
    await page.getByTitle("研发部", { exact: true }).getByLabel("Checkbox").check();
    await page.getByTitle("研发一组", { exact: true }).getByLabel("Checkbox").check();
    
    await page.getByRole("button", { name: "保 存" }).click();
    await expect(page.getByText("保存成功")).toBeVisible();
  });

  // RBAC-SCOPE-003: deptadmin.a searches users, only sees 研发一组
  test("RBAC-SCOPE-003 DeptAdminA 登录，仅能看到授权部门数据", async ({ page, loginAsDeptAdminA }) => {
    await loginAsDeptAdminA();
    await page.goto("/settings/org/users");
    
    // Should see user.a
    await page.getByPlaceholder("搜索...").fill("user.a.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    await page.waitForTimeout(500);
    await expect(page.locator("tr", { hasText: "user.a.e2e" })).toBeVisible();

    // Should NOT see user.b
    await page.getByPlaceholder("搜索...").fill("user.b.e2e");
    await page.getByPlaceholder("搜索...").press("Enter");
    await page.waitForTimeout(500);
    await expect(page.locator("tr", { hasText: "user.b.e2e" })).not.toBeVisible();
  });

  // RBAC-SCOPE-006: deptadmin.a searches users, only sees 研发二组 after scope change
  test("RBAC-SCOPE-006 修改自定义范围为二组后，DeptAdminA 的可见数据刷新", async ({ browser }) => {
    // We use isolated contexts here because we need superadmin to change things
    const context1 = await browser.newContext();
    const page1 = await context1.newPage();
    const { loginAsSuperAdmin } = require("../../fixtures/auth.fixture");
    // Pseudo-code inline setup, normally we'd isolate this better but for a demonstration run:
    
    // As SuperAdmin
    await page1.goto("/login");
    // Quick login via API not natively available outside fixture injects, 
    // so we just do UI login for SuperAdmin
    const tenantId = process.env.E2E_TEST_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
    await page1.getByPlaceholder("请输入租户").fill(tenantId);
    await page1.getByPlaceholder("用户名").fill("superadmin.e2e");
    await page1.getByPlaceholder("密码").fill(process.env.E2E_TEST_PASSWORD || "P@ssw0rd!");
    await page1.getByRole("button", { name: "登录" }).click();
    
    await page1.goto("/settings/auth/roles");
    await page1.getByPlaceholder("搜索角色...").fill("DeptAdminA");
    await page1.getByPlaceholder("搜索角色...").press("Enter");
    await page1.waitForTimeout(500);

    const row = page1.locator("tr").filter({ hasText: "DeptAdminA" });
    await row.getByRole("button", { name: "配置权限", exact: false }).click();
    await page1.getByRole('tab', { name: '数据范围' }).click();

    await page1.getByPlaceholder("请选择自定义部门").click();
    await page1.getByTitle("研发一组", { exact: true }).getByLabel("Checkbox").uncheck();
    await page1.getByTitle("研发二组", { exact: true }).getByLabel("Checkbox").check();
    await page1.getByRole("button", { name: "保 存" }).click();
    await page1.waitForTimeout(500);
    await context1.close();

    // Now switch to DeptAdminA context again
    const context2 = await browser.newContext();
    const page2 = await context2.newPage();
    await page2.goto("/login");
    await page2.getByPlaceholder("请输入租户").fill(tenantId);
    await page2.getByPlaceholder("用户名").fill("deptadmin.a.e2e");
    await page2.getByPlaceholder("密码").fill(process.env.E2E_TEST_PASSWORD || "P@ssw0rd!");
    await page2.getByRole("button", { name: "登录" }).click();
    
    await page2.goto("/settings/org/users");
    // Should see user.b (二组)
    await page2.getByPlaceholder("搜索...").fill("user.b.e2e");
    await page2.getByPlaceholder("搜索...").press("Enter");
    await page2.waitForTimeout(500);
    await expect(page2.locator("tr", { hasText: "user.b.e2e" })).toBeVisible();

    // Should NOT see user.a (一组)
    await page2.getByPlaceholder("搜索...").fill("user.a.e2e");
    await page2.getByPlaceholder("搜索...").press("Enter");
    await page2.waitForTimeout(500);
    await expect(page2.locator("tr", { hasText: "user.a.e2e" })).not.toBeVisible();
    await context2.close();
  });
});
