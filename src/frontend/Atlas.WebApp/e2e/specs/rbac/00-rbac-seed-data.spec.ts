import { test, expect } from "@playwright/test";

test.describe("RBAC 前置数据准备 (API 方式)", () => {

  test("00. 初始化必须的测试部门与角色", async ({ page }) => {
    const tenantId = process.env.E2E_TEST_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
    const adminPassword = process.env.E2E_TEST_PASSWORD ?? "P@ssw0rd!";
    
    // 1. 获取 Admin Token
    const loginResp = await page.request.post("/api/v1/auth/token", {
      headers: { "Content-Type": "application/json", "X-Tenant-Id": tenantId },
      data: { username: "admin", password: adminPassword },
    });
    expect(loginResp.ok(), "Admin login failed").toBeTruthy();
    const { accessToken } = (await loginResp.json()).data;
    const authHeaders = {
      "Authorization": `Bearer ${accessToken}`,
      "X-Tenant-Id": tenantId,
      "Content-Type": "application/json"
    };

    // 2. Fetch or Create Dept "研发部"
    const deptResp = await page.request.get("/api/v1/departments?pageSize=100", { headers: authHeaders });
    const depts = (await deptResp.json()).data?.items || [];
    let rDDept = depts.find((d: any) => d.name === "研发部");
    
    if (!rDDept) {
      const createDept = await page.request.post("/api/v1/departments", {
        headers: { ...authHeaders, "Idempotency-Key": `dept-rnd-${Date.now()}` },
        data: { name: "研发部", parentId: null, sortOrder: 1, isEnabled: true }
      });
      rDDept = (await createDept.json()).data;
    }

    // 3. Fetch Roles
    const roleResp = await page.request.get("/api/v1/roles?pageSize=100", { headers: authHeaders });
    const roles = (await roleResp.json()).data?.items || [];
    let deptAdminARole = roles.find((r: any) => r.code === "DeptAdminA") || roles.find((r: any) => r.code === "Admin"); // Fallback if seeding not run
    
    // 4. Create Users (DeptAdminA, user.a, user.b, readonly, others)
    const usersToCreate = [
      { 
        username: "deptadmin.a.e2e", 
        name: "测试部门领导A", 
        phone: "13800000001", 
        roleIds: deptAdminARole ? [deptAdminARole.id] : [],
        departmentId: rDDept.id
      },
      { username: "user.a.e2e", name: "测试员工user.a", phone: "13800000002" },
      { username: "user.b.e2e", name: "测试员工user.b", phone: "13800000003" },
      { username: "readonly.e2e", name: "只读测试账号", phone: "13800000004" },
      { username: "sysadmin.e2e", name: "系统管理员测试", phone: "13800000005" },
      { username: "securityadmin.e2e", name: "安全管理员测试", phone: "13800000006" },
      { username: "approvaladmin.e2e", name: "审批管理员测试", phone: "13800000007" },
    ];

    for (const u of usersToCreate) {
      // Check if exists
      const checkResp = await page.request.get(`/api/v1/users?keyword=${u.username}`, { headers: authHeaders });
      const existing = (await checkResp.json()).data?.items || [];
      
      // If the user already exists, delete it so we can recreate it with a known password and clean state
      if (existing.length > 0) {
        const userId = existing[0].id;
        await page.request.delete(`/api/v1/users/${userId}`, { headers: authHeaders });
      }

      // Create the user fresh
      await page.request.post("/api/v1/users", {
        headers: { ...authHeaders, "Idempotency-Key": `user-${u.username}-${Date.now()}` },
        data: {
          username: u.username,
          displayName: u.name,
          phoneNumber: u.phone,
          password: "P@ssw0rd!",
          gender: 1,
          accountType: 1,
          isEnabled: true,
          departmentIds: u.departmentId ? [u.departmentId] : [],
          roleIds: u.roleIds || [],
          positionIds: []
        }
      });
    }
    
    console.log("RBAC Data Seeding via API Complete.");
  });
});
