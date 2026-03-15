import { test, expect } from '@playwright/test';

test.describe('Atlas Security Platform GUI Tests', () => {
  // Use a fixed user for testing
  const tenantId = '00000000-0000-0000-0000-000000000001';
  const username = 'admin';
  const password = 'P@ssw0rd!';

  test.beforeEach(async ({ page }) => {
    // Optionally setup before each
  });

  test('1. 测试系统登录功能', async ({ page }) => {
    await page.goto('/login');
    // Ensure login page is loaded
    await expect(page.getByRole('heading', { name: '账号登录' })).toBeVisible();

    // Fill in credentials
    await page.getByPlaceholder('手机号 / 邮箱 / 用户名').fill(username);
    await page.getByPlaceholder('请输入密码').fill(password);
    
    // Click login
    await page.getByRole('button', { name: '登录' }).click();

    // Verify successful login by checking for a typical authenticated element, like user profile or dashboard
    await expect(page.getByRole('button', { name: '退出登录' }).or(page.locator('.ant-layout-header'))).toBeVisible({ timeout: 10000 });
  });

  test.describe('Authenticated Tests', () => {
    test.beforeEach(async ({ page }) => {
      // Login before each authenticated test
      await page.goto('/login');
      await page.getByPlaceholder('手机号 / 邮箱 / 用户名').fill(username);
      await page.getByPlaceholder('请输入密码').fill(password);
      await page.getByRole('button', { name: '登录' }).click();
      await page.waitForURL('**/system/notifications', { timeout: 10000 }).catch(() => {});
      await page.waitForTimeout(1000); // give it a moment to settle
    });

    test('2. 测试角色管理模块 (Roles)', async ({ page }) => {
      // 2.1 角色列表加载
      await page.goto('/settings/auth/roles');
      await expect(page.getByText('角色管理', { exact: true })).toBeVisible({ timeout: 10000 });
      const table = page.locator('.ant-table-wrapper').first();
      await expect(table).toBeVisible({ timeout: 10000 });

      // 2.2 角色搜索功能
      const searchBox = page.getByPlaceholder('请输入角色名称/编码搜索');
      await searchBox.fill('管理员');
      await page.keyboard.press('Enter');
      await page.waitForTimeout(1000);

      // 2.3 新建角色
      await page.getByRole('button', { name: '新增角色' }).click();
      const modal = page.locator('.ant-modal').filter({ hasText: '新建角色' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('角色名称').fill('GUI自动测试角色');
      await modal.getByLabel('角色编码').fill('GUI_AUTO_ROLE');
      await modal.locator('textarea[id="form_item_description"]').fill('由Playwright自动化测试创建');
      await modal.getByRole('button', { name: '确定' }).click();
      await expect(page.getByText('操作成功')).toBeVisible();

      // 2.4 编辑角色 (assuming it appears in the list)
      // Reset search first to find the new role
      await searchBox.clear();
      await searchBox.fill('GUI自动测试角色');
      await page.keyboard.press('Enter');
      await page.waitForTimeout(1000);
      
      const row = page.locator('.ant-table-row').filter({ hasText: /GUI自动/ }).first();
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-modal').filter({ hasText: '编辑角色' });
      await editModal.getByLabel('角色名称').fill('GUI自动测试角色-改');
      await editModal.getByRole('button', { name: '确定' }).click();
      await expect(page.getByText('操作成功')).toBeVisible();

      // 2.5 角色权限分配
      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '权限' }).click();
      const permDrawer = page.locator('.ant-drawer').filter({ hasText: '分配权限' });
      await expect(permDrawer).toBeVisible();
      await permDrawer.getByRole('button', { name: '保存' }).click(); // Just save for now
      await expect(page.getByText('操作成功')).toBeVisible();
      
      // 2.6 删除角色
      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '确认' }).or(page.getByRole('button', { name: 'Yes' })).click();
      await expect(page.getByText('操作成功')).toBeVisible();
    });

    test('3. 测试菜单管理模块 (Menus)', async ({ page }) => {
      // 3.1 菜单树列表加载
      await page.goto('/settings/auth/menus');
      await expect(page.getByText('菜单管理', { exact: true })).toBeVisible({ timeout: 10000 });
      const table = page.locator('.ant-table-wrapper').first();
      await expect(table).toBeVisible({ timeout: 10000 });

      // 3.2 新建菜单
      await page.getByRole('button', { name: '新增菜单' }).click();
      const modal = page.locator('.ant-drawer').filter({ hasText: '新增菜单' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('菜单名称').fill('GUI自动菜单');
      await modal.getByLabel('菜单路径').fill('/gui-auto-menu');
      await modal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('创建成功'))).toBeVisible();

      // 3.3 编辑菜单
      await page.waitForTimeout(1000);
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动菜单' });
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-drawer').filter({ hasText: '编辑菜单' });
      await editModal.getByLabel('菜单名称').fill('GUI自动菜单-改');
      await editModal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('更新成功'))).toBeVisible();
    });

    test('4. 测试用户管理模块 (Users)', async ({ page }) => {
      // 4.1 用户列表加载
      await page.goto('/settings/org/users');
      await expect(page.getByText('员工管理', { exact: true })).toBeVisible({ timeout: 10000 });
      const table = page.locator('.ant-table-wrapper').first();
      await expect(table).toBeVisible({ timeout: 10000 });

      // 4.2 新建用户
      await page.getByRole('button', { name: '新增员工' }).click();
      const modal = page.locator('.ant-drawer').filter({ hasText: '新增员工' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('用户名').fill('guiautouser');
      await modal.getByLabel('密码').fill('P@ssw0rd!');
      await modal.getByLabel('姓名').fill('GUI自动用户');
      await modal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('创建成功'))).toBeVisible();

      // 4.3 编辑用户
      await page.waitForTimeout(1000);
      const searchBox = page.getByPlaceholder('搜索用户名/姓名/邮箱');
      await searchBox.fill('guiautouser');
      await page.keyboard.press('Enter');
      await page.waitForTimeout(1000);
      
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动用户' });
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-drawer').filter({ hasText: '编辑员工' });
      await editModal.getByLabel('姓名').fill('GUI自动用户-改');
      await editModal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('更新成功'))).toBeVisible();

      // 4.4 删除用户
      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '删除' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('删除成功'))).toBeVisible();
    });

    test('5. 测试部门管理模块 (Departments)', async ({ page }) => {
      // For fallback paths
      await page.goto('/settings/org/departments').catch(() => {});
      await page.waitForTimeout(1000);
      await expect(page.getByText('部门管理', { exact: true })).toBeVisible({ timeout: 10000 });
      
      await page.getByRole('button', { name: '新增部门' }).click();
      const modal = page.locator('.ant-drawer').filter({ hasText: '新增部门' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('部门名称').fill('GUI自动部门');
      await modal.getByLabel('部门编码').fill('GUI_AUTO_DEPT');
      await modal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('创建成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动部门' });
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-drawer').filter({ hasText: '编辑部门' });
      await editModal.getByLabel('部门名称').fill('GUI自动部门-改');
      await editModal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('更新成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '删除' }).or(page.getByRole('button', { name: 'Yes' })).click();
      await expect(page.getByText('操作成功').or(page.getByText('删除成功'))).toBeVisible();
    });

    test('6. 测试职位管理模块 (Positions)', async ({ page }) => {
      await page.goto('/settings/org/positions').catch(() => {});
      await page.waitForTimeout(1000);
      await expect(page.getByText('职位管理', { exact: true })).toBeVisible({ timeout: 10000 });
      
      await page.getByRole('button', { name: '新增职位' }).click();
      const modal = page.locator('.ant-drawer').filter({ hasText: '新增职位' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('职位名称').fill('GUI自动职位');
      await modal.getByLabel('职位编码').fill('GUI_AUTO_POS');
      await modal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('创建成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动职位' });
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-drawer').filter({ hasText: '编辑职位' });
      await editModal.getByLabel('职位名称').fill('GUI自动职位-改');
      await editModal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('更新成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '删除' }).or(page.getByRole('button', { name: 'Yes' })).click();
      await expect(page.getByText('操作成功').or(page.getByText('删除成功'))).toBeVisible();
    });

    test('7. 测试项目管理模块 (Projects)', async ({ page }) => {
      await page.goto('/settings/projects').catch(() => {});
      await page.waitForTimeout(1000);
      await expect(page.getByText('项目管理', { exact: true })).toBeVisible({ timeout: 10000 });
      
      await page.getByRole('button', { name: '新增项目' }).click();
      const modal = page.locator('.ant-drawer').filter({ hasText: '新增项目' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('项目名称').fill('GUI自动项目');
      await modal.getByLabel('项目编码').fill('GUI_AUTO_PROJ');
      await modal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('创建成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动项目' });
      await expect(row).toBeVisible();
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-drawer').filter({ hasText: '编辑项目' });
      await editModal.getByLabel('项目名称').fill('GUI自动项目-改');
      await editModal.getByRole('button', { name: '保存' }).click();
      await expect(page.getByText('操作成功').or(page.getByText('更新成功'))).toBeVisible();

      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '删除' }).or(page.getByRole('button', { name: 'Yes' })).click();
      await expect(page.getByText('操作成功').or(page.getByText('删除成功'))).toBeVisible();
    });

    test('8. 测试应用管理模块 (Apps)', async ({ page }) => {
      await page.goto('/lowcode/apps').catch(() => {});
      await page.waitForTimeout(1000);
      await expect(page.getByText('低代码应用', { exact: true })).toBeVisible({ timeout: 10000 });
      
      await page.getByRole('button', { name: '新建应用' }).click();
      const modal = page.locator('.ant-modal').filter({ hasText: '新建应用' });
      await expect(modal).toBeVisible({ timeout: 5000 });
      await modal.getByLabel('应用名称').fill('GUI自动应用');
      await modal.getByLabel('应用标识').fill('GUI_AUTO_APP');
      await modal.getByRole('button', { name: '确认' }).or(page.getByRole('button', { name: '确定' })).click();
      
      await page.waitForTimeout(1000);
      // Builder interface might be triggered, go back
      await page.goto('/lowcode/apps');
      await page.waitForTimeout(1000);

      const appCard = page.locator('.app-card').filter({ hasText: 'GUI自动应用' });
      await expect(appCard).toBeVisible();
      
      await appCard.locator('.app-card-actions button').click(); 
      await page.getByRole('menuitem', { name: '编辑' }).click();
      const editModal = page.locator('.ant-modal').filter({ hasText: /应用设置|编辑应用/ });
      await expect(editModal).toBeVisible();
      await editModal.getByLabel('应用名称').fill('GUI自动应用-改');
      await editModal.getByRole('button', { name: '确认' }).or(page.getByRole('button', { name: '确定' })).click();

      await page.waitForTimeout(1000);
      await appCard.locator('.app-card-actions button').click();
      await page.getByRole('menuitem', { name: '删除' }).click();
      await page.getByRole('button', { name: '删除' }).or(page.getByRole('button', { name: '确认' })).or(page.getByRole('button', { name: '确定' })).click();
    });

    test('9. 测试租户管理模块 (Tenants)', async ({ page }) => {
      await page.goto('/settings/org/tenants').catch(() => {});
      await page.waitForTimeout(1000);
      await expect(page.getByText('租户管理', { exact: true })).toBeVisible({ timeout: 10000 });
      
      await page.getByRole('button', { name: '新建租户' }).click();
      const modal = page.locator('.ant-modal').filter({ hasText: '新建租户' });
      await expect(modal).toBeVisible();
      await modal.getByLabel('租户名称').fill('GUI自动租户');
      await modal.getByLabel('租户编码').fill('GUI_AUTO_TENANT');
      await modal.getByLabel('描述').fill('这是一个自动创建的租户');
      await modal.getByRole('button', { name: '确 定' }).or(page.getByRole('button', { name: '确 认' })).or(modal.locator('button.ant-btn-primary')).click();
      await expect(page.getByText('新建租户成功')).toBeVisible();

      await page.waitForTimeout(1000);
      const row = page.locator('.ant-table-row').filter({ hasText: 'GUI自动租户' });
      await expect(row).toBeVisible();
      
      await row.getByRole('button', { name: '编辑' }).click();
      const editModal = page.locator('.ant-modal').filter({ hasText: '编辑租户' });
      await editModal.getByLabel('租户名称').fill('GUI自动租户-改');
      await editModal.getByRole('button', { name: '确 定' }).or(page.getByRole('button', { name: '确 认' })).or(editModal.locator('button.ant-btn-primary')).click();
      await expect(page.getByText('编辑租户成功')).toBeVisible();

      await page.waitForTimeout(1000);
      await row.getByRole('button', { name: '删除' }).click();
      await page.getByRole('button', { name: '确定' }).or(page.getByRole('button', { name: '确认' })).click();
      await expect(page.getByText('删除成功')).toBeVisible();
    });
  });
});
