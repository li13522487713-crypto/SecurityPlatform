import { expect, test } from "../fixtures/single-session";
import type { APIRequestContext, Page } from "@playwright/test";
import { selectWorkspacePath } from "@atlas/app-shell-shared";
import {
  appBaseUrl,
  defaultTenantId,
  ensureAppSetup,
  expectNoI18nKeyLeak,
  navigateBySidebar,
  uniqueName
} from "./helpers";

const platformApiBase = "http://127.0.0.1:5001";

function authHeaders(accessToken: string): Record<string, string> {
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${accessToken}`,
    "X-Tenant-Id": defaultTenantId
  };
}

async function loginPlatformAccessToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${platformApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: "admin",
      password: "P@ssw0rd!"
    }
  });
  expect(response.ok()).toBeTruthy();
  const payload = (await response.json()) as {
    data?: { accessToken?: string };
  };
  const accessToken = String(payload?.data?.accessToken ?? "").trim();
  expect(accessToken).not.toBe("");
  return accessToken;
}

async function ensureWorkspacePair(request: APIRequestContext): Promise<Array<{ id: string; name: string }>> {
  const accessToken = await loginPlatformAccessToken(request);
  const listResponse = await request.get(`${platformApiBase}/api/v1/workspaces?page_num=1&page_size=20`, {
    headers: authHeaders(accessToken)
  });
  expect(listResponse.ok()).toBeTruthy();
  const listPayload = (await listResponse.json()) as {
    data?: { workspaces?: Array<{ id?: string | number; name?: string }> };
  };

  const workspaces = (listPayload.data?.workspaces ?? [])
    .map(item => ({
      id: String(item.id ?? "").trim(),
      name: String(item.name ?? "").trim()
    }))
    .filter(item => item.id);

  while (workspaces.length < 2) {
    const name = uniqueName("e2e-switch-workspace");
    const createResponse = await request.post(`${platformApiBase}/api/v1/workspaces`, {
      headers: authHeaders(accessToken),
      data: {
        name,
        description: "created by playwright workspace switch e2e"
      }
    });
    expect(createResponse.ok()).toBeTruthy();
    const createPayload = (await createResponse.json()) as {
      success?: boolean;
      code?: number | string;
      data?: { id?: string | number; workspace_id?: string | number };
    };
    const id = String(createPayload.data?.id ?? createPayload.data?.workspace_id ?? "").trim();
    expect(id).not.toBe("");
    workspaces.push({ id, name });
  }

  return workspaces.slice(0, 2);
}

async function openWorkspace(page: Page, workspaceId: string) {
  await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
  await page.getByTestId(`coze-select-workspace-${workspaceId}`).click();
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
}

async function switchWorkspace(page: Page, targetWorkspaceId: string) {
  await page.getByTestId("coze-workspace-switcher-trigger").click();
  await page.getByTestId(`coze-workspace-switcher-item-${targetWorkspaceId}`).click();
}

test.describe.serial("@smoke Workspace Switch", () => {
  let appKey = "";
  let workspaceIds: string[] = [];

  test.setTimeout(180_000);

  test.beforeAll(async ({ request, ensureLoggedInSession: ensureSession }) => {
    appKey = await ensureAppSetup(request);
    workspaceIds = (await ensureWorkspacePair(request)).map(item => item.id);
    await ensureSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await openWorkspace(page, workspaceIds[0]);
  });

  test("在项目开发中切换空间时保持当前菜单", async ({ page }) => {
    await navigateBySidebar(page, "projects", { pageTestId: "coze-projects-page" });
    await switchWorkspace(page, workspaceIds[1]);
    await expect(page).toHaveURL(new RegExp(`/workspace/${workspaceIds[1]}/projects$`));
    await expect(page.getByTestId("coze-projects-page")).toBeVisible();
    await expectNoI18nKeyLeak(page, "coze-projects-page");
  });

  test("命中详情型路径时切换空间会自动降级到菜单根路径", async ({ page }) => {
    await page.goto(`${appBaseUrl}/workspace/${workspaceIds[0]}/projects/folder/non-existent-folder`);
    await expect(page.getByTestId("coze-projects-page")).toBeVisible({ timeout: 30_000 });
    await switchWorkspace(page, workspaceIds[1]);
    await expect(page).toHaveURL(new RegExp(`/workspace/${workspaceIds[1]}/projects$`));
  });

  test("在平台菜单切换空间时保持原菜单路径不变", async ({ page }) => {
    await navigateBySidebar(page, "templates", { pageTestId: "coze-market-templates-page" });
    await switchWorkspace(page, workspaceIds[1]);
    await expect(page).toHaveURL(/\/market\/templates$/);
    await expect(page.getByTestId("coze-market-templates-page")).toBeVisible();
  });
});
