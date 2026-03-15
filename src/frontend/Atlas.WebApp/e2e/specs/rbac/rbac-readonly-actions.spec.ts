import { test, expect } from "../../fixtures/auth.fixture";

test.describe("RBAC - 只读审计角色 (ReadOnlyAuditor) 的 GUI 写操作限制验证", () => {
  // 确保测试串行执行
  test.describe.configure({ mode: 'serial' });

  test("1. 路由拦截: 验证只读账号直接访问角色管理页面被拦截", async ({ page, loginAsReadonly }) => {
    // 强制 GUI 登录
    await loginAsReadonly();
    
    // 直访无权限的写操作相关配置页面
    await page.goto("/settings/auth/roles");
    
    // 给路由守护加载反应时间
    await page.waitForTimeout(2000);
    
    // DEBUG: Dump the page content so we know exactly what is rendered
    console.log("=== DOM CONTENT LOG START ===");
    console.log(await page.content());
    console.log("=== DOM CONTENT LOG END ===");
    
    // 验证跳回 /console 或显示 403 页面
    const url = page.url();
    if (url.includes("/settings/auth/roles")) {
       await expect(
         page.locator("text=403")
         .or(page.locator("text=抱歉，您无权访问该页面"))
         .or(page.locator("text=404"))
         .or(page.locator("text=不存在"))
       ).toBeVisible();
    } else {
       expect(url).not.toContain("/settings/auth/roles");
    }
  });

  test("2. 操作拦截: 验证只读账号在日志页面只能查看不能操作", async ({ page, loginAsReadonly }) => {
    await loginAsReadonly();
    
    // 审计员应该有权限进入通知中心 (作为只读查看)
    await page.goto("/notices/center");
    await page.waitForLoadState("networkidle");
    
    // 由于是只读，页面不应渲染 "发布公告" 或类似的新增/编辑/删除按钮
    await expect(page.getByRole("button", { name: "发布公告" })).not.toBeVisible();
    await expect(page.getByRole("button", { name: "新增" })).not.toBeVisible();
    await expect(page.getByRole("button", { name: "删除" })).not.toBeVisible();
    
    // 审计员应该能够看到基础表格容器（证明查权限是有的）
    await expect(page.locator('.ant-table-wrapper').first()).toBeVisible({ timeout: 10000 });
  });
});
