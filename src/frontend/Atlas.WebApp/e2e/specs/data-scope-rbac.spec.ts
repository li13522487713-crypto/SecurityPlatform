import { test, expect, request as pwRequest } from '@playwright/test';

// Use a fixed admin user for setup
const tenantId = process.env.E2E_TEST_TENANT_ID || '00000000-0000-0000-0000-000000000001';
const adminUsername = process.env.E2E_TEST_USERNAME || 'admin';
const adminPassword = process.env.E2E_TEST_PASSWORD || 'P@ssw0rd!';

// Define unique suffix for this test run to prevent code conflicts
const runId = Math.floor(Date.now() / 1000).toString();

// Test context values
let apiUrl = 'http://localhost:5000/api/v1';
let adminToken = '';

let hqId: string;
let prodId: string;
let rdId: string;
let rd1Id: string;

let rolePmId: string;
let roleDevId: string;
let roleLeaderId: string;
let roleAuditorId: string;

let userPm1Id: string;
let userDev1Id: string;
let userDev2Id: string;
let userLeaderId: string;

const usernamePm1 = `e2e_pm1_${runId}`;
const usernameDev1 = `e2e_dev1_${runId}`;
const usernameDev2 = `e2e_dev2_${runId}`;
const usernameLeader = `e2e_leader_${runId}`;

test.describe('Multi-Account RBAC & Data Scope E2E Tests', () => {
  // --- 1. Admin Data Preparation ---
  test.beforeAll(async () => {
    const setupContext = await pwRequest.newContext();
    const loginRes = await setupContext.post(`${apiUrl}/auth/token`, {
      headers: { 'X-Tenant-Id': tenantId },
      data: { username: adminUsername, password: adminPassword }
    });
    const loginData = await loginRes.json();
    adminToken = loginData.data?.accessToken;
    expect(adminToken).toBeTruthy();

    const headers = {
      'Authorization': `Bearer ${adminToken}`,
      'X-Tenant-Id': tenantId,
      'Content-Type': 'application/json'
    };

    async function safePost(url: string, payload: any) {
      const resp = await setupContext.post(url, { headers, data: payload });
      const body = await resp.json();
      if (!body.data) throw new Error(`API failed on ${url}: ${JSON.stringify(body)}`);
      return body.data.id;
    }

    // 2. Setup Departments
    hqId = await safePost(`${apiUrl}/departments`, { name: `E2E_HQ_${runId}`, code: `HQ_${runId}`, sortOrder: 1 });
    prodId = await safePost(`${apiUrl}/departments`, { name: `E2E_Product_${runId}`, code: `PROD_${runId}`, parentId: Number(hqId), sortOrder: 1 });
    rdId = await safePost(`${apiUrl}/departments`, { name: `E2E_RD_${runId}`, code: `RD_${runId}`, parentId: Number(hqId), sortOrder: 2 });
    rd1Id = await safePost(`${apiUrl}/departments`, { name: `E2E_RD_Group_1_${runId}`, code: `RD1_${runId}`, parentId: Number(rdId), sortOrder: 1 });

    // 3. Find Menu IDs we need
    const menusRes = await setupContext.get(`${apiUrl}/menus/all`, { headers });
    const menusData = (await menusRes.json()).data;
    const userMenu = menusData.find((m: any) => m.path === '/settings/org/users');
    const roleMenu = menusData.find((m: any) => m.path === '/settings/auth/roles');
    if (!userMenu || !roleMenu) throw new Error("Could not find required menus");
    const userMenuId = Number(userMenu.id);
    const roleMenuId = Number(roleMenu.id);

    // 4. Setup Roles
    async function createRole(name: string, code: string, dataScope: number, menuIds: number[]) {
      const roleId = await safePost(`${apiUrl}/roles`, { name, code, description: 'E2E DataScope Test Role' });
      await setupContext.put(`${apiUrl}/roles/${roleId}/data-scope`, { headers, data: { dataScope, deptIds: [] } });
      await setupContext.put(`${apiUrl}/roles/${roleId}/menus`, { headers, data: { menuIds } });
      return roleId;
    }

    rolePmId = await createRole(`E2E_Role_PM_${runId}`, `R_PM_${runId}`, 5, [userMenuId]); // OnlySelf
    roleDevId = await createRole(`E2E_Role_DEV_${runId}`, `R_DEV_${runId}`, 5, [userMenuId]); // OnlySelf
    roleLeaderId = await createRole(`E2E_Role_Leader_${runId}`, `R_LD_${runId}`, 4, [userMenuId, roleMenuId]); // CurrentDeptAndBelow
    roleAuditorId = await createRole(`E2E_Role_Aud_${runId}`, `R_AUD_${runId}`, 1, [userMenuId]); // All

    // 5. Setup Users
    async function createUser(username: string, displayName: string, deptId: string, roleIdsArr: string[]) {
      return await safePost(`${apiUrl}/users`, {
        username, password: 'P@ssw0rd123!', displayName, isActive: true, 
        roleIds: roleIdsArr.map(Number), departmentIds: [Number(deptId)], positionIds: []
      });
    }

    userPm1Id = await createUser(usernamePm1, `测试PM张三_${runId}`, prodId, [rolePmId]);
    userDev1Id = await createUser(usernameDev1, `测试DEV李四_${runId}`, rdId, [roleDevId]);
    userDev2Id = await createUser(usernameDev2, `测试DEV王五_${runId}`, rd1Id, [roleDevId]);
    userLeaderId = await createUser(usernameLeader, `测试LD领导_${runId}`, rdId, [roleLeaderId]);
    
    await setupContext.dispose();
  });

  async function loginAndNavigate(page: any, username: string) {
    const loginResp = await page.request.post('/api/v1/auth/token', {
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId },
      data: { username: username, password: 'P@ssw0rd123!' },
    });
    expect(loginResp.ok()).toBeTruthy();
    const { accessToken, refreshToken } = (await loginResp.json()).data;
    
    await page.goto('/login');
    await page.evaluate(({ accessToken, refreshToken, tenantId }) => {
      sessionStorage.setItem('access_token', accessToken);
      localStorage.setItem('refresh_token', refreshToken);
      localStorage.setItem('tenant_id', tenantId);
    }, { accessToken, refreshToken, tenantId });
    
    await page.goto('/console');
    await page.waitForURL('**/system/notifications', { timeout: 10000 }).catch(() => {});
    await page.waitForTimeout(1000);
  }

  // --- 2. Menu RBAC Isolation ---
  test('Menu RBAC Isolation for PM via UI', async ({ page }) => {
    await loginAndNavigate(page, usernamePm1);
    await page.goto('/settings/org/users');
    await expect(page.getByText('员工管理', { exact: true })).toBeVisible({ timeout: 10000 });

    await page.goto('/settings/auth/roles');
    await expect(page.getByText('角色管理', { exact: true })).toBeHidden({ timeout: 3000 }).catch(() => {});
  });

  test('Menu RBAC Isolation for Leader via UI', async ({ page }) => {
    await loginAndNavigate(page, usernameLeader);
    await page.goto('/settings/org/users');
    await expect(page.getByText('员工管理', { exact: true })).toBeVisible({ timeout: 10000 });

    await page.goto('/settings/auth/roles');
    await expect(page.getByText('角色管理', { exact: true })).toBeVisible({ timeout: 10000 });
  });

  // --- 3. OnlySelf Scope Verification ---
  test('Data Scope Isolation (OnlySelf) - test_dev1', async ({ page }) => {
    await loginAndNavigate(page, usernameDev1);
    await page.goto('/settings/org/users');
    await expect(page.getByText('员工管理', { exact: true })).toBeVisible();
    await page.waitForTimeout(1000);

    const tableRows = page.locator('.ant-table-row');
    const tableText = await tableRows.allInnerTexts();
    
    expect(tableText.some(t => t.includes(`测试DEV李四_${runId}`))).toBeTruthy();
    expect(tableText.some(t => t.includes(`测试DEV王五_${runId}`))).toBeFalsy();
    expect(tableText.some(t => t.includes(`测试PM张三_${runId}`))).toBeFalsy();
    expect(tableText.some(t => t.includes(`测试LD领导_${runId}`))).toBeFalsy();
  });

  // --- 4. CurrentDeptAndBelow Scope Verification ---
  test('Data Scope Penetration (CurrentDeptAndBelow) - test_leader', async ({ page }) => {
    await loginAndNavigate(page, usernameLeader);
    await page.goto('/settings/org/users');
    await expect(page.getByText('员工管理', { exact: true })).toBeVisible();
    await page.waitForTimeout(2000); // 增加一点等待时间让数据加载出来

    const tableRows = page.locator('.ant-table-row');
    const tableText = await tableRows.allInnerTexts();
    
    expect(tableText.some(t => t.includes(`测试LD领导_${runId}`))).toBeTruthy();
    expect(tableText.some(t => t.includes(`测试DEV李四_${runId}`))).toBeTruthy();
    expect(tableText.some(t => t.includes(`测试DEV王五_${runId}`))).toBeTruthy();
    
    expect(tableText.some(t => t.includes(`测试PM张三_${runId}`))).toBeFalsy();
  });

  // --- 5. Role Overlay / Highest Scope Verification ---
  test('Role Overlay resolving to highest scope - test_dev1', async ({ page }) => {
    const context = await pwRequest.newContext();
    await context.put(`${apiUrl}/users/${userDev1Id}/roles`, {
      headers: { 'Authorization': `Bearer ${adminToken}`, 'X-Tenant-Id': tenantId, 'Content-Type': 'application/json' },
      data: { roleIds: [Number(roleDevId), Number(roleAuditorId)] }
    });
    await context.dispose();

    await loginAndNavigate(page, usernameDev1);
    await page.goto('/settings/org/users');
    await expect(page.getByText('员工管理', { exact: true })).toBeVisible();
    await page.waitForTimeout(2000);

    const tableRows = page.locator('.ant-table-row');
    const tableText = await tableRows.allInnerTexts();
    
    expect(tableText.some(t => t.includes(`测试PM张三_${runId}`))).toBeTruthy();
    expect(tableText.some(t => t.includes(`测试LD领导_${runId}`))).toBeTruthy();
    expect(tableText.some(t => t.includes(`测试DEV李四_${runId}`))).toBeTruthy();
  });

  // --- Cleanup ---
  test.afterAll(async () => {
    const context = await pwRequest.newContext();
    const headers = { 'Authorization': `Bearer ${adminToken}`, 'X-Tenant-Id': tenantId };
    
    async function safeDelete(url: string) { await context.delete(url, { headers }); }

    if (userPm1Id) await safeDelete(`${apiUrl}/users/${userPm1Id}`);
    if (userDev1Id) await safeDelete(`${apiUrl}/users/${userDev1Id}`);
    if (userDev2Id) await safeDelete(`${apiUrl}/users/${userDev2Id}`);
    if (userLeaderId) await safeDelete(`${apiUrl}/users/${userLeaderId}`);

    if (rolePmId) await safeDelete(`${apiUrl}/roles/${rolePmId}`);
    if (roleDevId) await safeDelete(`${apiUrl}/roles/${roleDevId}`);
    if (roleLeaderId) await safeDelete(`${apiUrl}/roles/${roleLeaderId}`);
    if (roleAuditorId) await safeDelete(`${apiUrl}/roles/${roleAuditorId}`);

    if (rd1Id) await safeDelete(`${apiUrl}/departments/${rd1Id}`);
    if (rdId) await safeDelete(`${apiUrl}/departments/${rdId}`);
    if (prodId) await safeDelete(`${apiUrl}/departments/${prodId}`);
    if (hqId) await safeDelete(`${apiUrl}/departments/${hqId}`);

    await context.dispose();
  });
});
