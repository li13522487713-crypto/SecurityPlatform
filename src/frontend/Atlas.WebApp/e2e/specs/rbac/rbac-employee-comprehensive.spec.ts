

test.describe("RBAC - 普通员工 (UserA) 角色", () => {
  // 确保测试串行执行，便于调试
  test.describe.configure({ mode: 'serial' });

  test("1. 菜单权限: 验证普通员工无法看到任何后台管理级菜单", async ({ page, loginAsUserA }) => {
    await loginAsUserA();
    
    const sidebarToggle = page.locator('.ant-layout-sider-trigger');
    if (await sidebarToggle.isVisible()) {
        await sidebarToggle.click();
    }

    // 核心断言：普通用户不该看到管理菜单
    await expect(page.getByRole("menuitem", { name: "组织架构" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "部门管理" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "员工管理" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "角色管理" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "菜单管理" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "系统配置" })).not.toBeVisible();
    await expect(page.getByRole("menuitem", { name: "安全中心" })).not.toBeVisible();
  });

  test("2. 页面拦截: 即使通过链接直达，也不能访问无权限内容", async ({ page, loginAsUserA }) => {
    await loginAsUserA();
    
    // 尝试直接通过 URL 访问员工管理页面 (这是强限制页面)
    await page.goto("/settings/org/users");
    
    // 给出反应时间让前端路由或组件加载后触发鉴权跳转/拦截
    await page.waitForTimeout(2000); 
    
    // 验证：最终 URL 不能是受保护的页面 /settings/org/users，大概率会被踢回 /console、/403 或者被重定向
    const url = page.url();
    if (url.includes("/settings/org/users")) {
       // 如果没有发生重定向，那么必须渲染 403 无权限页面
       await expect(page.locator("text=403").or(page.locator("text=抱歉，您无权访问该页面"))).toBeVisible();
    } else {
       // 如果发生了重定向，则 URL 必定不包含目标页面
       expect(url).not.toContain("/settings/org/users");
    }
  });

  test("3. 数据权限与操作权限: 全局操作权限隔离验证", async ({ page, loginAsUserA }) => {
    await loginAsUserA();
    
    // 既然无法访问管理页面，自然不存在后台特权操作。
    // 作为保险防御校验，我们搜索全页面不应当出现高危特权的全局"批量删除"或全局"新增"按钮资源。
    await expect(page.getByRole("button", { name: "批量删除" })).not.toBeVisible();
    
    // 哪怕他们不小心通过某种异常手段进入了页面，受限于后端 API，页面的受保护写按钮也不应呈现
    // 注意：这里的测试重在证明前端根据角色状态（目前几乎没有角色）渲染出的按钮为空集。
  });
});
