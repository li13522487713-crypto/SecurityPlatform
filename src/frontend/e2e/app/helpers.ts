import path from "node:path";
import { expect, type APIRequestContext, type Page, type TestInfo } from "@playwright/test";
import {
  communityWorksPath,
  docsPath,
  marketPluginsPath,
  marketTemplatesPath,
  openApiPath,
  platformGeneralPath,
  selectWorkspacePath,
  workspaceEvaluationsPath,
  workspaceHomePath,
  workspaceProjectsPath,
  workspaceResourcesPath,
  workspaceSettingsModelsPath,
  workspaceSettingsPublishPath,
  workspaceTasksPath,
  signPath
} from "@atlas/app-shell-shared";
import { gazeShiftDelay, randomBetween, thinkingPause } from "../fixtures/human-mouse";

const appWebPort = process.env.PLAYWRIGHT_APP_WEB_PORT ?? "5181";
const platformWebPort = process.env.PLAYWRIGHT_PLATFORM_WEB_PORT ?? appWebPort;
export const platformBaseUrl = `http://127.0.0.1:${platformWebPort}`;
export const appBaseUrl = `http://127.0.0.1:${appWebPort}`;
export const platformApiBase = "http://127.0.0.1:5001";
export const appApiBase = "http://127.0.0.1:5002";
const appWebMode = (process.env.PLAYWRIGHT_APP_WEB_MODE ?? "platform").toLowerCase();
const usesPlatformControlPlane = appWebMode === "platform";

export const defaultTenantId = "00000000-0000-0000-0000-000000000001";
export const defaultUsername = "admin";
export const defaultPassword = "P@ssw0rd!";

const e2eRunSeed = process.env.PLAYWRIGHT_E2E_RUN_ID ?? `${Date.now()}-${process.pid}`;
const platformDatabasePath = `Data Source=${path.resolve(process.cwd(), `../backend/Atlas.PlatformHost/atlas.e2e.platform.${e2eRunSeed}.db`)}`;
const appDatabasePath = `Data Source=${path.resolve(process.cwd(), `../backend/Atlas.AppHost/atlas.e2e.app.${e2eRunSeed}.db`)}`;
const appName = "App E2E Regression";
const e2eDataSourceName = "App E2E DataSource";

interface ApiResponse<T> {
  success?: boolean;
  message?: string;
  data?: T;
}

interface AuthTokenData {
  accessToken: string;
}

interface PagedResult<T> {
  items: T[];
}

interface TenantAppInstanceListItem {
  id: string;
  appKey: string;
  name: string;
}

interface TenantAppInstanceDetail {
  id: string;
  name: string;
  description?: string | null;
  category?: string | null;
  icon?: string | null;
  dataSourceId?: string | null;
}

interface TenantDataSourceDto {
  id: string;
  name: string;
  isActive?: boolean;
}

function toHeaders(accessToken: string, withIdempotencyPrefix?: string): Record<string, string> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Authorization: `Bearer ${accessToken}`,
    "X-Tenant-Id": defaultTenantId
  };
  void withIdempotencyPrefix;
  return headers;
}

async function loginPlatformApi(request: APIRequestContext): Promise<string> {
  const tokenResp = await request.post(`${platformApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });
  const tokenPayload = (await tokenResp.json()) as ApiResponse<AuthTokenData>;
  expect(tokenResp.ok()).toBeTruthy();
  expect(tokenPayload?.data?.accessToken).toBeTruthy();
  return tokenPayload.data!.accessToken;
}

async function ensureTenantDataSource(request: APIRequestContext, accessToken: string): Promise<number> {
  const listResp = await request.get(`${platformApiBase}/api/v1/tenant-datasources`, {
    headers: toHeaders(accessToken)
  });
  const listPayload = (await listResp.json()) as ApiResponse<TenantDataSourceDto[]>;
  expect(listResp.ok()).toBeTruthy();

  const dataSources = listPayload.data ?? [];
  const existing =
    dataSources.find((item) => item.name === e2eDataSourceName) ??
    dataSources.find((item) => item.isActive !== false) ??
    dataSources[0];
  if (existing) {
    return Number(existing.id);
  }

  const createResp = await request.post(`${platformApiBase}/api/v1/tenant-datasources`, {
    headers: toHeaders(accessToken, "app-e2e-ds-create"),
    data: {
      tenantIdValue: defaultTenantId,
      name: e2eDataSourceName,
      connectionString: appDatabasePath,
      dbType: "SQLite",
      ownershipScope: "Platform",
      mode: "raw",
      maxPoolSize: 50,
      connectionTimeoutSeconds: 15
    }
  });
  const createPayload = (await createResp.json()) as ApiResponse<{ Id?: string; id?: string }>;
  if (!createResp.ok() && createResp.status() === 403) {
    const fallbackListResp = await request.get(`${platformApiBase}/api/v1/tenant-datasources`, {
      headers: toHeaders(accessToken)
    });
    const fallbackListPayload = (await fallbackListResp.json()) as ApiResponse<TenantDataSourceDto[]>;
    const fallbackDataSource = fallbackListPayload.data?.find((item) => item.isActive !== false) ?? fallbackListPayload.data?.[0];
    if (fallbackDataSource) {
      return Number(fallbackDataSource.id);
    }
  }
  expect(createResp.ok()).toBeTruthy();
  expect(createPayload.success).toBeTruthy();
  const rawId = createPayload.data?.Id ?? createPayload.data?.id;
  expect(rawId).toBeTruthy();
  return Number(rawId);
}

async function getAppInstanceByKey(
  request: APIRequestContext,
  accessToken: string,
  appKey: string
): Promise<TenantAppInstanceListItem> {
  const listResp = await request.get(`${platformApiBase}/api/v2/tenant-app-instances?pageIndex=1&pageSize=200`, {
    headers: toHeaders(accessToken)
  });
  const listPayload = (await listResp.json()) as ApiResponse<PagedResult<TenantAppInstanceListItem>>;
  expect(listResp.ok()).toBeTruthy();

  const matched = (listPayload.data?.items ?? []).find((item) => item.appKey === appKey);
  expect(matched).toBeTruthy();
  return matched!;
}

async function ensureAppDataSourceBinding(
  request: APIRequestContext,
  appKey: string
): Promise<void> {
  const accessToken = await loginPlatformApi(request);
  const dataSourceId = await ensureTenantDataSource(request, accessToken);
  const appInstance = await getAppInstanceByKey(request, accessToken, appKey);

  const detailResp = await request.get(`${platformApiBase}/api/v2/tenant-app-instances/${appInstance.id}`, {
    headers: toHeaders(accessToken)
  });
  const detailPayload = (await detailResp.json()) as ApiResponse<TenantAppInstanceDetail>;
  expect(detailResp.ok()).toBeTruthy();
  expect(detailPayload.success).toBeTruthy();

  if (detailPayload.data?.dataSourceId === String(dataSourceId)) {
    return;
  }

  const updateResp = await request.put(`${platformApiBase}/api/v2/tenant-app-instances/${appInstance.id}`, {
    headers: toHeaders(accessToken, "app-e2e-app-bind-datasource"),
    data: {
      name: detailPayload.data?.name ?? appInstance.name,
      description: detailPayload.data?.description ?? null,
      category: detailPayload.data?.category ?? null,
      icon: detailPayload.data?.icon ?? null,
      dataSourceId,
      unbindDataSource: false
    }
  });
  const updatePayload = (await updateResp.json()) as ApiResponse<{ id?: string; Id?: string }>;
  expect(updateResp.ok()).toBeTruthy();
  expect(updatePayload.success).toBeTruthy();

  await expect
    .poll(
      async () => {
        const verifyResp = await request.get(`${platformApiBase}/api/v2/tenant-app-instances/${appInstance.id}`, {
          headers: toHeaders(accessToken)
        });
        const verifyPayload = (await verifyResp.json()) as ApiResponse<TenantAppInstanceDetail>;
        return verifyPayload.data?.dataSourceId ?? "";
      },
      { timeout: 30_000 }
    )
    .toBe(String(dataSourceId));
}

async function clearStorageForOrigin(page: Page, url: string) {
  let lastError: unknown = null;

  for (let attempt = 0; attempt < 3; attempt += 1) {
    await page.goto(url, { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("domcontentloaded");

    try {
      await page.evaluate(() => {
        window.localStorage.clear();
        window.sessionStorage.clear();
      });
      return;
    } catch (error) {
      lastError = error;
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes("Execution context was destroyed")) {
        throw error;
      }
      await page.waitForTimeout(200);
    }
  }

  if (lastError) {
    throw lastError;
  }
}

export async function clearAuthStorage(page: Page) {
  await page.context().clearCookies();
  await clearStorageForOrigin(page, appBaseUrl);
}

export async function seedLocale(page: Page, locale: "zh-CN" | "en-US") {
  await page.addInitScript((nextLocale) => {
    window.localStorage.setItem("atlas_locale", nextLocale);
  }, locale);
}

export async function ensurePlatformSetup(request: APIRequestContext) {
  const stateResp = await request.get(`${platformApiBase}/api/v1/setup/state`);
  const statePayload = await stateResp.json();
  if (statePayload?.success && statePayload?.data?.status === "Ready") {
    return;
  }

  const initializeResp = await request.post(`${platformApiBase}/api/v1/setup/initialize`, {
    timeout: 60_000,
    data: {
      database: {
        driverCode: "SQLite",
        mode: "raw",
        connectionString: platformDatabasePath
      },
      admin: {
        tenantId: defaultTenantId,
        username: defaultUsername,
        password: defaultPassword
      },
      roles: {
        selectedRoleCodes: ["SecurityAdmin"]
      },
      organization: {
        departments: [{ name: "总部", code: "HQ", parentCode: null, sortOrder: 0 }],
        positions: [{ name: "系统管理员", code: "SYS_ADMIN", description: "系统配置与运维管理", sortOrder: 10 }]
      }
    }
  });

  const initializePayload = await initializeResp.json();
  const initialized =
    (initializeResp.ok() && initializePayload?.success) ||
    initializePayload?.code === "ALREADY_CONFIGURED";
  expect(
    initialized,
    `platform setup initialize failed: status=${initializeResp.status()} payload=${JSON.stringify(initializePayload)}`
  ).toBeTruthy();

  await expect
    .poll(async () => {
      const response = await request.get(`${platformApiBase}/api/v1/setup/state`);
      const payload = await response.json();
      return payload?.data?.status;
    }, { timeout: 45_000 })
    .toBe("Ready");
}

export async function ensureAppSetup(request: APIRequestContext): Promise<string> {
  if (usesPlatformControlPlane) {
    await ensurePlatformSetup(request);
  }

  const stateResp = await request.get(`${appApiBase}/api/v1/setup/state`);
  const statePayload = await stateResp.json();
  if (statePayload?.success && statePayload?.data?.appSetupCompleted === true && statePayload?.data?.appKey) {
    return statePayload.data.appKey;
  }

  const initializeResp = await request.post(`${appApiBase}/api/v1/setup/initialize`, {
    timeout: 60_000,
    data: {
      database: {
        driverCode: "SQLite",
        mode: "raw",
        connectionString: appDatabasePath
      },
      admin: {
        appName,
        adminUsername: defaultUsername
      },
      roles: {
        selectedRoleCodes: ["SecurityAdmin"]
      },
      organization: {
        departments: [{ name: "总部", code: "HQ", parentCode: null, sortOrder: 0 }],
        positions: [{ name: "系统管理员", code: "SYS_ADMIN", description: "系统配置与运维管理", sortOrder: 10 }]
      }
    }
  });

  const initializePayload = await initializeResp.json();
  const initialized =
    (initializeResp.ok() && initializePayload?.success) ||
    initializePayload?.code === "ALREADY_CONFIGURED";
  expect(
    initialized,
    `app setup initialize failed: status=${initializeResp.status()} payload=${JSON.stringify(initializePayload)}`
  ).toBeTruthy();

  await expect
    .poll(async () => {
      const response = await request.get(`${appApiBase}/api/v1/setup/state`);
      const payload = await response.json();
      return payload?.data?.appSetupCompleted;
    }, { timeout: 45_000 })
    .toBeTruthy();

  const latestStateResp = await request.get(`${appApiBase}/api/v1/setup/state`);
  const latestStatePayload = await latestStateResp.json();
  const resolvedAppKey = String(latestStatePayload?.data?.appKey ?? process.env.PLAYWRIGHT_APP_KEY ?? "dev-app");
  return resolvedAppKey;
}

export async function resolveCanonicalAppKey(request: APIRequestContext): Promise<string> {
  const response = await request.get(`${appApiBase}/api/v1/setup/state`);
  const payload = await response.json();
  const appKey = String(payload?.data?.appKey ?? "").trim();
  if (appKey) return appKey;
  return String(process.env.PLAYWRIGHT_APP_KEY ?? "dev-app");
}

async function waitForLoginFormReady(page: Page, appKey: string): Promise<void> {
  void appKey;
  const loginPaths = [signPath()];
  const maxAttempts = loginPaths.length * 3;
  let lastUrl = page.url();

  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    const currentPath = loginPaths[attempt % loginPaths.length];
    await page.goto(`${appBaseUrl}${currentPath}`, { waitUntil: "domcontentloaded" });
    lastUrl = page.url();

    const usernameVisible = await page
      .getByTestId("app-login-username")
      .isVisible({ timeout: 5_000 })
      .catch(() => false);
    const passwordVisible = await page
      .getByTestId("app-login-password")
      .isVisible({ timeout: 5_000 })
      .catch(() => false);
    if (usernameVisible && passwordVisible) {
      return;
    }

    const loginPageVisible = await page
      .getByTestId("app-login-page")
      .isVisible({ timeout: 2_000 })
      .catch(() => false);
    if (loginPageVisible) {
      const usernameVisibleOnLoginPage = await page
        .getByTestId("app-login-username")
        .isVisible({ timeout: 5_000 })
        .catch(() => false);
      if (usernameVisibleOnLoginPage) {
        return;
      }
    }

    await page.waitForTimeout(350);
  }

  const diagnostics = await page.evaluate(() => {
    const testIds = Array.from(document.querySelectorAll("[data-testid]"))
      .slice(0, 20)
      .map((node) => node.getAttribute("data-testid"))
      .filter((item): item is string => Boolean(item));
    const bodyText = document.body?.innerText?.replace(/\s+/g, " ").trim().slice(0, 240) ?? "";
    return {
      href: window.location.href,
      readyState: document.readyState,
      testIds,
      bodyText
    };
  });

  throw new Error(
    `App 登录页未就绪，尝试路径: ${loginPaths.join(", ")}，最后 URL: ${lastUrl}，诊断: ${JSON.stringify(diagnostics)}`
  );
}

export async function loginApp(
  page: Page,
  appKey: string,
  password = defaultPassword,
  options?: { expectSuccess?: boolean }
) {
  const expectSuccess = options?.expectSuccess ?? true;
  await waitForLoginFormReady(page, appKey);
  await page.getByTestId("app-login-username").fill(defaultUsername);
  await page.waitForTimeout(gazeShiftDelay());
  await page.getByTestId("app-login-password").fill(password);
  await page.waitForTimeout(thinkingPause());
  await page.getByTestId("app-login-submit").click();

  if (!expectSuccess) {
    return;
  }

  // 新 IA：登录后要么停留在工作空间选择页，要么自动进入工作空间首页。
  const workspaceSelectPattern = new RegExp(`${selectWorkspacePath()}(?:\\?.*)?$`);
  const workspaceHomePattern = /\/workspace\/[^/]+\/home(?:\?.*)?$/;
  await page.waitForURL((url) => workspaceSelectPattern.test(url.pathname + url.search) || workspaceHomePattern.test(url.pathname + url.search), {
    timeout: 30_000
  });
  const onSelectPage = await page.getByTestId("coze-select-workspace-page").isVisible().catch(() => false);
  if (!onSelectPage) {
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
  }
}

export async function ensureAppWorkspace(page: Page, appKey: string) {
  const workspaceHomePattern = /\/workspace\/[^/]+\/home(?:\?.*)?$/;
  if (!workspaceHomePattern.test(page.url())) {
    await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
    await expect(page.getByTestId("coze-select-workspace-page")).toBeVisible({ timeout: 30_000 });

    const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
    if (await matchedWorkspaceButton.count()) {
      await matchedWorkspaceButton.click();
    } else {
      await page.locator('[data-testid^="coze-select-workspace-"]').first().click();
    }
  }

  await page.waitForURL(workspaceHomePattern, { timeout: 45_000 });
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
}

function getWorkspaceRouteContext(page: Page): { workspaceId: string } {
  const currentUrl = new URL(page.url());
  const match = currentUrl.pathname.match(/^\/workspace\/([^/]+)/);
  if (!match) {
    throw new Error(`当前页面不在工作区上下文中，无法解析 canonical 路由: ${currentUrl.pathname}`);
  }

  return {
    workspaceId: decodeURIComponent(match[1])
  };
}

function resolveSidebarAliasTarget(page: Page, itemKey: string): string | null {
  const { workspaceId } = getWorkspaceRouteContext(page);

  switch (itemKey) {
    case "home":
    case "dashboard":
      return workspaceHomePath(workspaceId);
    case "projects":
    case "develop":
    case "agents":
      return workspaceProjectsPath(workspaceId);
    case "resources":
    case "library":
      return workspaceResourcesPath(workspaceId);
    case "tasks":
    case "manage":
    case "users":
    case "roles":
    case "departments":
    case "positions":
    case "approval":
      return workspaceTasksPath(workspaceId);
    case "evaluations":
    case "reports":
    case "dashboards":
    case "visualization":
      return workspaceEvaluationsPath(workspaceId);
    case "settings":
    case "agent-chat":
    case "ai-assistant":
    case "publish-center":
      return workspaceSettingsPublishPath(workspaceId);
    case "model-configs":
      return workspaceSettingsModelsPath(workspaceId);
    case "workflows":
      return workspaceResourcesPath(workspaceId, "workflows");
    case "chatflows":
      return workspaceResourcesPath(workspaceId, "chatflows");
    case "knowledge-bases":
      return workspaceResourcesPath(workspaceId, "knowledge");
    case "databases":
    case "data":
      return workspaceResourcesPath(workspaceId, "databases");
    case "variables":
      return workspaceResourcesPath(workspaceId, "variables");
    case "templates":
      return marketTemplatesPath();
    case "plugins":
      return marketPluginsPath();
    case "community":
      return communityWorksPath();
    case "open-api":
      return openApiPath();
    case "docs":
      return docsPath();
    case "platform":
      return platformGeneralPath();
    default:
      return null;
  }
}

export async function navigateBySidebar(
  page: Page,
  itemKey: string,
  options: {
    pageTestId?: string;
    urlPattern?: RegExp;
  } = {}
) {
  const sidebar = page.getByTestId("app-sidebar");
  await expect(sidebar).toBeVisible({ timeout: 30_000 });
  const itemProbe = page.getByTestId(`app-sidebar-item-${itemKey}`);
  const pageTestId = options.pageTestId;

  if (!(await itemProbe.isVisible().catch(() => false))) {
    const moreButtons = sidebar.locator('[data-testid^="app-sidebar-section-more-"]');
    const count = await moreButtons.count();
    for (let index = 0; index < count; index += 1) {
      const button = moreButtons.nth(index);
      if (!(await button.isVisible().catch(() => false))) {
        continue;
      }

      await button.click();
      await page.waitForTimeout(randomBetween(40, 100));

      if (await itemProbe.isVisible().catch(() => false)) {
        break;
      }
    }
  }

  if (!(await itemProbe.isVisible().catch(() => false))) {
    const aliasTarget = resolveSidebarAliasTarget(page, itemKey);
    if (!aliasTarget) {
      throw new Error(`左侧菜单缺少 ${itemKey}，且未配置 canonical 兼容跳转。`);
    }

    await page.goto(`${appBaseUrl}${aliasTarget}`);
  } else {
    const item = page.getByTestId(`app-sidebar-item-${itemKey}`);
    await expect(item, `左侧菜单缺少 ${itemKey}，当前账号不具备对应权限或菜单未正确投影。`).toBeVisible({
      timeout: 30_000
    });

    await page.waitForTimeout(randomBetween(40, 100));
    await item.click();
  }

  if (pageTestId) {
    await expect(page.getByTestId(pageTestId)).toBeVisible({ timeout: 30_000 });
    return;
  }

  if (options.urlPattern) {
    await page.waitForURL(options.urlPattern, { timeout: 30_000 });
  }
}

export async function clickCrudSubmit(page: Page) {
  const visibleModalSubmit = page.locator(".semi-modal:visible .semi-modal-footer .semi-button-primary").last();
  const submit = await visibleModalSubmit.count()
    ? visibleModalSubmit
    : page.locator(".semi-modal-footer .semi-button-primary:visible").last();
  await expect(submit).toBeVisible({ timeout: 30_000 });
  await expect(submit).toBeEnabled({ timeout: 30_000 });
  await page.waitForTimeout(thinkingPause());
  await submit.click();
}

export async function waitForCrudDrawerClosed(page: Page, probeTestId?: string) {
  const drawerSubmit = page.locator(".semi-modal-footer .semi-button-primary").last();
  await expect(drawerSubmit).toBeHidden({ timeout: 30_000 });

  if (probeTestId) {
    await expect(page.getByTestId(probeTestId)).toBeHidden({ timeout: 30_000 });
  }

  await expect(page.locator(".semi-modal-content")).toBeHidden({ timeout: 30_000 });
}

export function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
}

export async function expectNoI18nKeyLeak(page: Page, rootTestId?: string) {
  const textContent = await page.evaluate((testId) => {
    const root = testId ? document.querySelector(`[data-testid="${testId}"]`) : document.body;
    return root?.textContent ?? "";
  }, rootTestId);

  const leakedKeys = Array.from(
    textContent.match(/\b[a-z][a-z0-9]*(?:\.[a-z][a-z0-9]*)+\b/g) ?? []
  ).filter((candidate) => !candidate.startsWith("http.") && !candidate.startsWith("https."));

  expect(leakedKeys, `检测到 i18n key 泄漏: ${leakedKeys.join(", ")}`).toEqual([]);
}

export async function captureEvidenceScreenshot(
  page: Page,
  testInfo: TestInfo,
  name: string
) {
  const normalizedName = name
    .trim()
    .replace(/[^a-zA-Z0-9-_]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
  const relativePath = `${normalizedName || "e2e-screenshot"}.png`;
  const screenshotPath = testInfo.outputPath(relativePath);
  await page.screenshot({ path: screenshotPath, fullPage: true });
  await testInfo.attach(`screenshot:${normalizedName || "e2e-screenshot"}`, {
    path: screenshotPath,
    contentType: "image/png"
  });
}
