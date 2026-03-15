import { request } from "@playwright/test";
import type { E2ERole, SeedAccount, SeedState, StoredAuthState } from "../catalog/seed-types";
import { SeedApiClient } from "./api-client";
import { writeAuthState, writeSeedState } from "./auth-state";

type IdValue = string | number;
type EntityItem = Record<string, unknown> & { id?: IdValue };

interface RoleSeedSpec {
  code: string;
  name: string;
  description: string;
  permissionCodes: "*" | string[];
  menuPaths: string[];
  dataScope?: { type: number; deptIds?: string[] };
}

const defaultTenantId = process.env.E2E_TEST_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
const defaultPassword = process.env.E2E_TEST_PASSWORD ?? "P@ssw0rd!";
const superadminUsername = process.env.E2E_SUPERADMIN_USERNAME ?? process.env.E2E_TEST_USERNAME ?? "admin";

const accountDefs: Record<E2ERole, Omit<SeedAccount, "tenantId">> = {
  superadmin: { role: "superadmin", username: superadminUsername, displayName: "E2E Super Admin", homePath: "/system/notifications", roleCodes: ["Admin", "SuperAdmin"] },
  sysadmin: { role: "sysadmin", username: process.env.E2E_SYSADMIN_USERNAME ?? "sysadmin.e2e", displayName: "E2E System Admin", homePath: "/settings/org/users", roleCodes: ["Admin"] },
  securityadmin: { role: "securityadmin", username: process.env.E2E_SECURITYADMIN_USERNAME ?? "securityadmin.e2e", displayName: "E2E Security Admin", homePath: "/assets", roleCodes: ["E2E_SECURITY_ADMIN"] },
  approvaladmin: { role: "approvaladmin", username: process.env.E2E_APPROVALADMIN_USERNAME ?? "approvaladmin.e2e", displayName: "E2E Approval Admin", homePath: "/approval/flows", roleCodes: ["E2E_APPROVAL_ADMIN"] },
  aiadmin: { role: "aiadmin", username: process.env.E2E_AIADMIN_USERNAME ?? "aiadmin.e2e", displayName: "E2E AI Admin", homePath: "/ai/agents", roleCodes: ["E2E_AI_ADMIN"] },
  appadmin: { role: "appadmin", username: process.env.E2E_APPADMIN_USERNAME ?? "appadmin.e2e", displayName: "E2E App Admin", homePath: "/lowcode/apps", roleCodes: ["E2E_APP_ADMIN"] },
  deptadminA: { role: "deptadminA", username: process.env.E2E_DEPTADMIN_A_USERNAME ?? "deptadmin.a.e2e", displayName: "E2E Dept Admin A", homePath: "/settings/org/users", roleCodes: ["E2E_DEPT_ADMIN_A"] },
  deptadminB: { role: "deptadminB", username: process.env.E2E_DEPTADMIN_B_USERNAME ?? "deptadmin.b.e2e", displayName: "E2E Dept Admin B", homePath: "/settings/org/users", roleCodes: ["E2E_DEPT_ADMIN_B"] },
  userA: { role: "userA", username: process.env.E2E_USER_A_USERNAME ?? "user.a.e2e", displayName: "E2E User A", homePath: "/system/notifications", roleCodes: [] },
  userB: { role: "userB", username: process.env.E2E_USER_B_USERNAME ?? "user.b.e2e", displayName: "E2E User B", homePath: "/system/notifications", roleCodes: [] },
  readonly: { role: "readonly", username: process.env.E2E_READONLY_USERNAME ?? "readonly.e2e", displayName: "E2E Readonly", homePath: "/system/notifications", roleCodes: [] }
};

function toStringId(value: IdValue | undefined) {
  return value === undefined ? "" : String(value);
}

function toIdList(values: Array<IdValue | undefined>) {
  return values.map((value) => toStringId(value)).filter((value) => value.length > 0);
}

function pickBy<T extends EntityItem>(items: T[], key: string, value: string) {
  return items.find((item) => String(item[key] ?? "") === value);
}

function extractItems(data: unknown): EntityItem[] {
  if (Array.isArray(data)) {
    return data as EntityItem[];
  }
  if (data && typeof data === "object" && Array.isArray((data as { items?: unknown[] }).items)) {
    return (data as { items: EntityItem[] }).items;
  }
  return [];
}

async function getItems(client: SeedApiClient, path: string) {
  const glue = path.includes("?") ? "&" : "?";
  const response = await client.get<unknown>(`${path}${glue}pageIndex=1&pageSize=500`);
  const paged = extractItems(response.data);
  if (paged.length > 0) {
    return paged;
  }

  const direct = await client.get<unknown>(path);
  return extractItems(direct.data);
}

async function findUserByUsername(client: SeedApiClient, username: string) {
  const keywordPath = `/users?pageIndex=1&pageSize=50&keyword=${encodeURIComponent(username)}`;
  const scoped = await client.get<unknown>(keywordPath);
  const hit = pickBy(extractItems(scoped.data), "username", username);
  if (hit) {
    return hit;
  }
  return pickBy(await getItems(client, "/users"), "username", username);
}

async function ensureCreated(
  client: SeedApiClient,
  listPath: string,
  lookupKey: string,
  lookupValue: string,
  createPath: string,
  payload: Record<string, unknown>
) {
  const existing = pickBy(await getItems(client, listPath), lookupKey, lookupValue);
  if (existing) {
    return existing;
  }

  await client.post(createPath, payload);
  const created = pickBy(await getItems(client, listPath), lookupKey, lookupValue);
  if (!created) {
    throw new Error(`Failed to create ${lookupValue}`);
  }
  return created;
}

async function trySeed<T>(factory: () => Promise<T>, fallback: T): Promise<T> {
  try {
    return await factory();
  } catch {
    return fallback;
  }
}

function buildRoleSpecs(deptRd: string, deptRdA: string, deptSecOps: string): RoleSeedSpec[] {
  return [
    {
      code: "E2E_SECURITY_ADMIN",
      name: "E2E Security Admin",
      description: "Security center access for E2E",
      permissionCodes: ["assets:view", "audit:view", "alert:view"],
      menuPaths: ["/assets", "/audit", "/alert", "/system/notifications"]
    },
    {
      code: "E2E_APPROVAL_ADMIN",
      name: "E2E Approval Admin",
      description: "Approval access for E2E",
      permissionCodes: "*",
      menuPaths: ["/approval/flows", "/approval/workspace", "/approval/designer", "/approval/flows/manage"]
    },
    {
      code: "E2E_AI_ADMIN",
      name: "E2E AI Admin",
      description: "AI access for E2E",
      permissionCodes: "*",
      menuPaths: ["/settings/ai/model-configs", "/ai/agents", "/ai/knowledge-bases", "/ai/databases", "/ai/plugins", "/ai/apps"]
    },
    {
      code: "E2E_APP_ADMIN",
      name: "E2E App Admin",
      description: "Lowcode access for E2E",
      permissionCodes: "*",
      menuPaths: ["/console", "/console/apps", "/lowcode/apps", "/lowcode/forms", "/lowcode/plugin-market"]
    },
    {
      code: "E2E_DEPT_ADMIN_A",
      name: "E2E Dept Admin A",
      description: "Department scoped admin A",
      permissionCodes: "*",
      menuPaths: ["/settings/org/departments", "/settings/org/users", "/system/notifications"],
      dataScope: { type: 2, deptIds: [deptRd, deptRdA] }
    },
    {
      code: "E2E_DEPT_ADMIN_B",
      name: "E2E Dept Admin B",
      description: "Department scoped admin B",
      permissionCodes: "*",
      menuPaths: ["/settings/org/departments", "/settings/org/users", "/system/notifications"],
      dataScope: { type: 3, deptIds: [deptSecOps] }
    }
  ];
}

async function ensureCustomRoles(client: SeedApiClient, specs: RoleSeedSpec[]) {
  const permissions = await getItems(client, "/permissions");
  const menus = await trySeed(() => getItems(client, "/menus/all"), await getItems(client, "/menus"));
  const roles = await getItems(client, "/roles");
  const permissionMap = new Map(permissions.map((item) => [String(item.code), item]));
  const menuMap = new Map(menus.map((item) => [String(item.path), item]));
  const roleMap = new Map(roles.map((item) => [String(item.code), item]));
  const resolved: Record<string, string> = {};

  for (const spec of specs) {
    const role =
      roleMap.get(spec.code) ??
      (await ensureCreated(client, "/roles", "code", spec.code, "/roles", {
        code: spec.code,
        name: spec.name,
        description: spec.description
      }));

    const permissionIds =
      spec.permissionCodes === "*"
        ? toIdList(Array.from(permissionMap.values()).map((item) => item.id))
        : toIdList(spec.permissionCodes.map((code) => permissionMap.get(code)?.id));
    const menuIds = toIdList(spec.menuPaths.map((path) => menuMap.get(path)?.id));
    const roleId = toStringId(role.id);
    if (!roleId) {
      continue;
    }

    await trySeed(() => client.put(`/roles/${roleId}/permissions`, { permissionIds }), undefined);
    await trySeed(() => client.put(`/roles/${roleId}/menus`, { menuIds }), undefined);
    if (spec.dataScope) {
      await trySeed(() => client.put(`/roles/${roleId}/data-scope`, { dataScope: spec.dataScope.type, deptIds: spec.dataScope.deptIds ?? [] }), undefined);
    }

    resolved[spec.code] = roleId;
  }

  return resolved;
}

async function createStoredAuthState(apiBaseUrl: string, role: E2ERole, account: SeedAccount): Promise<StoredAuthState> {
  const context = await request.newContext();
  try {
    const client = new SeedApiClient(context, apiBaseUrl, account.tenantId);
    await client.login(account.username, defaultPassword);
    const profile = await client.loadProfile();
    await client.loadAntiforgeryToken();
    const storageState = await client.storageState();

    return {
      role,
      username: account.username,
      tenantId: account.tenantId,
      homePath: account.homePath,
      permissions: profile.permissions,
      roles: profile.roles,
      cookies: storageState.cookies.map((cookie) => ({
        name: cookie.name,
        value: cookie.value,
        domain: cookie.domain,
        path: cookie.path,
        expires: cookie.expires,
        httpOnly: cookie.httpOnly,
        secure: cookie.secure,
        sameSite: cookie.sameSite
      })),
      localStorage: {
        tenant_id: account.tenantId,
        refresh_token: client.getRefreshToken() ?? ""
      },
      sessionStorage: {
        access_token: client.getAccessToken() ?? "",
        antiforgery_token: client.getCsrfToken() ?? "",
        auth_profile: JSON.stringify({
          username: profile.username,
          displayName: profile.displayName,
          permissions: profile.permissions,
          roles: profile.roles
        })
      }
    };
  } finally {
    await context.dispose();
  }
}

export async function seedE2EState(apiBaseUrl: string): Promise<SeedState> {
  const requestContext = await request.newContext();
  try {
    const client = new SeedApiClient(requestContext, apiBaseUrl, defaultTenantId);
    await client.login(superadminUsername, defaultPassword);
    await client.loadAntiforgeryToken();

    const secondaryTenantId = await trySeed(async () => {
      const tenant = pickBy(await getItems(client, "/tenants"), "code", "E2E_TENANT_2");
      if (tenant) return toStringId(tenant.id);
      const created = await client.post<number>("/tenants", { code: "E2E_TENANT_2", name: "E2E Tenant 2", remark: "Created by E2E" });
      return toStringId(created.data as IdValue | undefined);
    }, "");

    const deptHeadquarters = await ensureCreated(client, "/departments", "name", "E2E HQ", "/departments", { name: "E2E HQ", sortOrder: 1 });
    const deptRd = await ensureCreated(client, "/departments", "name", "E2E RD", "/departments", { name: "E2E RD", parentId: toStringId(deptHeadquarters.id), sortOrder: 10 });
    const deptRdA = await ensureCreated(client, "/departments", "name", "E2E RD A", "/departments", { name: "E2E RD A", parentId: toStringId(deptRd.id), sortOrder: 11 });
    const deptRdB = await ensureCreated(client, "/departments", "name", "E2E RD B", "/departments", { name: "E2E RD B", parentId: toStringId(deptRd.id), sortOrder: 12 });
    const deptSecOps = await ensureCreated(client, "/departments", "name", "E2E SecOps", "/departments", { name: "E2E SecOps", parentId: toStringId(deptHeadquarters.id), sortOrder: 20 });
    const deptFinance = await ensureCreated(client, "/departments", "name", "E2E Finance", "/departments", { name: "E2E Finance", parentId: toStringId(deptHeadquarters.id), sortOrder: 30 });

    const posSecLead = await ensureCreated(client, "/positions", "code", "E2E_SEC_LEAD", "/positions", { code: "E2E_SEC_LEAD", name: "E2E Security Lead", isActive: true, sortOrder: 100 });
    const posAppLead = await ensureCreated(client, "/positions", "code", "E2E_APP_LEAD", "/positions", { code: "E2E_APP_LEAD", name: "E2E App Lead", isActive: true, sortOrder: 110 });
    const posAnalyst = await ensureCreated(client, "/positions", "code", "E2E_ANALYST", "/positions", { code: "E2E_ANALYST", name: "E2E Analyst", isActive: true, sortOrder: 120 });

    const projectA = await ensureCreated(client, "/projects", "code", "E2E_PROJ_A", "/projects", { code: "E2E_PROJ_A", name: "E2E Project A", isActive: true, sortOrder: 10 });
    const projectB = await ensureCreated(client, "/projects", "code", "E2E_PROJ_B", "/projects", { code: "E2E_PROJ_B", name: "E2E Project B", isActive: true, sortOrder: 20 });

    const customRoles = await trySeed(() => ensureCustomRoles(client, buildRoleSpecs(toStringId(deptRd.id), toStringId(deptRdA.id), toStringId(deptSecOps.id))), {} as Record<string, string>);
    const roles = await getItems(client, "/roles");
    let adminRoleId = toStringId(pickBy(roles, "code", "Admin")?.id);
    if (!adminRoleId) {
      const securityAdminRoleId = toStringId(pickBy(roles, "code", "SecurityAdmin")?.id);
      if (securityAdminRoleId) {
        for (let offset = 1n; offset <= 4n; offset += 1n) {
          const candidateId = (BigInt(securityAdminRoleId) - offset).toString();
          const detail = await trySeed(() => client.get<{ code?: string }>(`/roles/${candidateId}`), { data: undefined } as { data?: { code?: string } });
          if (detail.data?.code === "Admin") {
            adminRoleId = candidateId;
            break;
          }
        }
      }
    }
    const fallbackAdminRoleId = adminRoleId || customRoles.E2E_APP_ADMIN || customRoles.E2E_DEPT_ADMIN_A || customRoles.E2E_SECURITY_ADMIN || "";
    const securityAdminRoleId = customRoles.E2E_SECURITY_ADMIN || fallbackAdminRoleId;
    const approvalAdminRoleId = customRoles.E2E_APPROVAL_ADMIN || fallbackAdminRoleId;
    const aiAdminRoleId = customRoles.E2E_AI_ADMIN || fallbackAdminRoleId;
    const appAdminRoleId = customRoles.E2E_APP_ADMIN || fallbackAdminRoleId;
    const deptAdminARoleId = customRoles.E2E_DEPT_ADMIN_A || fallbackAdminRoleId;
    const deptAdminBRoleId = customRoles.E2E_DEPT_ADMIN_B || fallbackAdminRoleId;

    const accounts = Object.fromEntries(Object.entries(accountDefs).map(([role, def]) => [role, { ...def, tenantId: defaultTenantId }])) as Record<E2ERole, SeedAccount>;
    const staticPhones: Record<E2ERole, string> = {
      superadmin: "13900000001",
      sysadmin: "13900000002",
      securityadmin: "13900000003",
      approvaladmin: "13900000004",
      aiadmin: "13900000005",
      appadmin: "13900000006",
      deptadminA: "13900000007",
      deptadminB: "13900000008",
      userA: "13900000009",
      userB: "13900000010",
      readonly: "13900000011"
    };

    const roleIdsByUser: Record<E2ERole, string[]> = {
      superadmin: toIdList([fallbackAdminRoleId]),
      sysadmin: toIdList([fallbackAdminRoleId]),
      securityadmin: toIdList([securityAdminRoleId]),
      approvaladmin: toIdList([approvalAdminRoleId]),
      aiadmin: toIdList([aiAdminRoleId]),
      appadmin: toIdList([appAdminRoleId]),
      deptadminA: toIdList([deptAdminARoleId]),
      deptadminB: toIdList([deptAdminBRoleId]),
      userA: [],
      userB: [],
      readonly: []
    };

    const departmentIdsByUser: Record<E2ERole, string[]> = {
      superadmin: [toStringId(deptHeadquarters.id)],
      sysadmin: [toStringId(deptHeadquarters.id)],
      securityadmin: [toStringId(deptSecOps.id)],
      approvaladmin: [toStringId(deptRd.id)],
      aiadmin: [toStringId(deptRd.id)],
      appadmin: [toStringId(deptRd.id)],
      deptadminA: [toStringId(deptRd.id)],
      deptadminB: [toStringId(deptSecOps.id)],
      userA: [toStringId(deptRdA.id)],
      userB: [toStringId(deptRdB.id)],
      readonly: [toStringId(deptFinance.id)]
    };

    const positionIdsByUser: Record<E2ERole, string[]> = {
      superadmin: [toStringId(posSecLead.id)],
      sysadmin: [toStringId(posSecLead.id)],
      securityadmin: [toStringId(posSecLead.id)],
      approvaladmin: [toStringId(posAnalyst.id)],
      aiadmin: [toStringId(posAppLead.id)],
      appadmin: [toStringId(posAppLead.id)],
      deptadminA: [toStringId(posAnalyst.id)],
      deptadminB: [toStringId(posSecLead.id)],
      userA: [toStringId(posAnalyst.id)],
      userB: [toStringId(posAnalyst.id)],
      readonly: [toStringId(posAnalyst.id)]
    };

    for (const role of Object.keys(accounts) as E2ERole[]) {
      const account = accounts[role];
      if (role === "superadmin") {
        continue;
      }

      const existing = await findUserByUsername(client, account.username);
      const payload = {
        username: account.username,
        password: defaultPassword,
        displayName: account.displayName,
        email: `${account.username}@example.com`,
        phoneNumber: staticPhones[role],
        isActive: true,
        roleIds: roleIdsByUser[role],
        departmentIds: departmentIdsByUser[role],
        positionIds: positionIdsByUser[role]
      };

      if (existing) {
        const userId = toStringId(existing.id);
        if (userId) {
          await trySeed(() => client.delete(`/users/${userId}`), undefined);
        }
      }

      await client.post("/users", payload);
    }

    const usersAfter = await getItems(client, "/users");
    const sysadminUser = pickBy(usersAfter, "username", accounts.sysadmin.username);
    const deptAdminAUser = pickBy(usersAfter, "username", accounts.deptadminA.username);
    const userAUser = pickBy(usersAfter, "username", accounts.userA.username);
    await trySeed(() => client.put(`/projects/${toStringId(projectA.id)}/users`, { userIds: toIdList([sysadminUser?.id, deptAdminAUser?.id, userAUser?.id]) }), undefined);
    await trySeed(() => client.put(`/projects/${toStringId(projectA.id)}/departments`, { departmentIds: toIdList([deptRd.id, deptRdA.id]) }), undefined);
    await trySeed(() => client.put(`/projects/${toStringId(projectB.id)}/users`, { userIds: toIdList([sysadminUser?.id]) }), undefined);
    await trySeed(() => client.put(`/projects/${toStringId(projectB.id)}/departments`, { departmentIds: toIdList([deptSecOps.id]) }), undefined);

    const dataSourceId = await trySeed(async () => {
      const items = await getItems(client, "/tenant-datasources");
      const existing = pickBy(items, "name", "E2E Sqlite");
      if (existing) return toStringId(existing.id);
      const created = await client.post<{ Id?: string; id?: string }>("/tenant-datasources", { tenantIdValue: defaultTenantId, name: "E2E Sqlite", dbType: "Sqlite", connectionString: "Data Source=atlas-e2e-tenant.db" });
      return toStringId(created.data?.id ?? created.data?.Id);
    }, "");

    const lowCodeApps = {
      e2e_console_app: await trySeed(async () => toStringId((await ensureCreated(client, "/lowcode-apps", "appKey", "e2e_console_app", "/lowcode-apps", { appKey: "e2e_console_app", name: "E2E Console App", description: "Seeded by E2E", category: "E2E", icon: "appstore", dataSourceId: dataSourceId || undefined, useSharedUsers: true, useSharedRoles: true, useSharedDepartments: true })).id), ""),
      e2e_workspace_app: await trySeed(async () => toStringId((await ensureCreated(client, "/lowcode-apps", "appKey", "e2e_workspace_app", "/lowcode-apps", { appKey: "e2e_workspace_app", name: "E2E Workspace App", description: "Seeded by E2E", category: "E2E", icon: "appstore", dataSourceId: dataSourceId || undefined, useSharedUsers: true, useSharedRoles: true, useSharedDepartments: true })).id), "")
    };

    const homePageId = await trySeed(async () => {
      if (!lowCodeApps.e2e_workspace_app) return "";
      const pages = await getItems(client, `/lowcode-apps/${lowCodeApps.e2e_workspace_app}/pages/tree`);
      const existing = pickBy(pages, "pageKey", "home");
      if (existing) return toStringId(existing.id);
      const created = await client.post<{ Id?: string; id?: string }>(`/lowcode-apps/${lowCodeApps.e2e_workspace_app}/pages`, { pageKey: "home", name: "E2E Home", pageType: "Page", schemaJson: JSON.stringify({ type: "page", title: "E2E Home", body: [{ type: "tpl", tpl: "E2E Home" }] }), routePath: "/home", description: "Seeded E2E home page", icon: "file", sortOrder: 1 });
      return toStringId(created.data?.id ?? created.data?.Id);
    }, "");

    const notificationId = await trySeed(async () => toStringId((await ensureCreated(client, "/notifications/manage", "title", "E2E Announcement", "/notifications/manage", { title: "E2E Announcement", content: "Seeded by E2E", noticeType: "Announcement", priority: 1 })).id), "");
    const dictTypeId = await trySeed(async () => toStringId((await ensureCreated(client, "/dict-types", "code", "E2E_STATUS", "/dict-types", { code: "E2E_STATUS", name: "E2E Status", status: true })).id), "");
    const systemConfigId = await trySeed(async () => {
      const byKey = await trySeed(() => client.get<EntityItem>("/system-configs/by-key/e2e.feature.switch"), { data: undefined } as { data?: EntityItem });
      if (byKey.data?.id) return toStringId(byKey.data.id);
      const created = await client.post<{ Id?: string; id?: string }>("/system-configs", { configKey: "e2e.feature.switch", configValue: "enabled", configName: "E2E Feature Switch" });
      return toStringId(created.data?.id ?? created.data?.Id);
    }, "");
    const webhookId = await trySeed(async () => {
      const items = await getItems(client, "/webhooks");
      const existing = pickBy(items, "name", "E2E Webhook");
      if (existing) return toStringId(existing.id);
      const created = await client.post<{ Id?: string; id?: string }>("/webhooks", { name: "E2E Webhook", eventTypes: ["system.notification.created"], targetUrl: "https://example.com/e2e-webhook", secret: "e2e-secret" });
      return toStringId(created.data?.id ?? created.data?.Id);
    }, "");

    const ai = {
      modelConfigId: await trySeed(async () => toStringId((await getItems(client, "/model-configs"))[0]?.id) || undefined, undefined),
      agentId: await trySeed(async () => toStringId((await getItems(client, "/agents"))[0]?.id) || undefined, undefined),
      knowledgeBaseId: await trySeed(async () => toStringId((await getItems(client, "/knowledge-bases"))[0]?.id) || undefined, undefined),
      databaseId: await trySeed(async () => toStringId((await getItems(client, "/ai-databases"))[0]?.id) || undefined, undefined),
      pluginId: await trySeed(async () => toStringId((await getItems(client, "/ai-plugins"))[0]?.id) || undefined, undefined),
      aiAppId: await trySeed(async () => toStringId((await getItems(client, "/ai-apps"))[0]?.id) || undefined, undefined),
      variableId: await trySeed(async () => toStringId((await getItems(client, "/ai-variables"))[0]?.id) || undefined, undefined),
      promptId: await trySeed(async () => toStringId((await getItems(client, "/ai-prompts"))[0]?.id) || undefined, undefined),
      shortcutId: await trySeed(async () => toStringId((await getItems(client, "/ai-shortcuts"))[0]?.id) || undefined, undefined),
      marketplaceId: await trySeed(async () => toStringId((await getItems(client, "/ai-marketplace/apps"))[0]?.id) || undefined, undefined)
    };

    const approval = await trySeed(async () => {
      const flows = await getItems(client, "/approval/flows");
      const instances = await trySeed(() => getItems(client, "/approval/runtime/instances"), [] as EntityItem[]);
      const tasks = await trySeed(() => getItems(client, "/approval/tasks"), [] as EntityItem[]);
      return { flowId: toStringId(flows[0]?.id), instanceId: toStringId(instances[0]?.id), taskId: toStringId(tasks[0]?.id), copyId: "" };
    }, { flowId: "", instanceId: "", taskId: "", copyId: "" });

    const state: SeedState = {
      generatedAt: new Date().toISOString(),
      baseTenantId: defaultTenantId,
      secondaryTenantId,
      accounts,
      departments: { hq: toStringId(deptHeadquarters.id), rd: toStringId(deptRd.id), rdA: toStringId(deptRdA.id), rdB: toStringId(deptRdB.id), secOps: toStringId(deptSecOps.id), finance: toStringId(deptFinance.id) },
      positions: { E2E_SEC_LEAD: toStringId(posSecLead.id), E2E_APP_LEAD: toStringId(posAppLead.id), E2E_ANALYST: toStringId(posAnalyst.id) },
      roles: customRoles,
      projects: { E2E_PROJ_A: toStringId(projectA.id), E2E_PROJ_B: toStringId(projectB.id) },
      dataSources: { E2E_SQLITE: dataSourceId },
      lowCodeApps,
      lowCodeAppKeys: { e2e_console_app: "e2e_console_app", e2e_workspace_app: "e2e_workspace_app" },
      pages: { home: homePageId },
      ai,
      approval,
      notifications: { notificationId },
      dictionaries: { dictTypeId },
      configs: { systemConfigId },
      webhooks: { webhookId: webhookId || undefined }
    };

    writeSeedState(state);
    for (const role of Object.keys(accounts) as E2ERole[]) {
      try {
        writeAuthState(role, await createStoredAuthState(apiBaseUrl, role, accounts[role]));
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to create auth state for ${role}: ${message}`);
      }
    }

    return state;
  } finally {
    await requestContext.dispose();
  }
}
