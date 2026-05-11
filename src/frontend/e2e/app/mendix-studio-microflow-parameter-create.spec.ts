import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultPassword, defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

function readWorkspaceIdFromUrl(url: string): string | undefined {
  return url.match(/\/space\/([^/]+)/)?.[1]
    ?? url.match(/\/workspace\/([^/]+)/)?.[1];
}

let authenticatedAccessToken = "";

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string) {
  const name = uniqueName("E2E_MF_PARAM_CREATE").replace(/-/g, "_");
  const response = await page.request.post(`${appApiBase}/api/v1/microflows`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId,
      "X-Workspace-Id": workspaceId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
    data: {
      workspaceId,
      input: {
        name,
        displayName: name,
        moduleId: "Sales",
        moduleName: "Sales",
        tags: ["e2e", "parameter-create"],
        parameters: [],
        returnType: { kind: "void" },
        template: "blank",
      },
    },
  });
  expect(response.ok()).toBeTruthy();

  const listResponse = await page.request.get(`${appApiBase}/api/v1/microflows?workspaceId=${encodeURIComponent(workspaceId)}`, {
    headers: {
      "X-Tenant-Id": defaultTenantId,
      "X-Workspace-Id": workspaceId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
  });
  expect(listResponse.ok()).toBeTruthy();
  const listJson = await listResponse.json();
  const created = (listJson?.data?.items ?? []).find((item: { name?: string; id?: string }) => item.name === name);
  expect(created?.id).toBeTruthy();
  return { id: String(created.id), name };
}

async function loginForParameterCreate(page: import("@playwright/test").Page, appKey: string) {
  const tokenResp = await page.request.post(`${appApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId,
    },
    data: {
      username: "admin",
      password: defaultPassword,
    },
  });
  expect(tokenResp.ok()).toBeTruthy();
  const tokenPayload = await tokenResp.json();
  const accessToken = String(tokenPayload?.data?.accessToken ?? "");
  expect(accessToken).not.toBe("");
  authenticatedAccessToken = accessToken;

  const meResp = await page.request.get(`${appApiBase}/api/v1/auth/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "X-Tenant-Id": defaultTenantId,
    },
  });
  expect(meResp.ok()).toBeTruthy();
  const mePayload = await meResp.json();

  await page.goto(`${appBaseUrl}${signPath()}`, { waitUntil: "domcontentloaded" });
  await page.evaluate(({ token, tenantId, profile }) => {
    window.localStorage.setItem("atlas_app_access_token", token);
    window.localStorage.setItem("atlas_app_tenant_id", tenantId);
    window.localStorage.removeItem("atlas_last_workspace_id");
    window.localStorage.removeItem("atlas_app_auth_profile");
    window.sessionStorage.setItem("atlas_app_auth_profile", JSON.stringify(profile));
  }, {
    token: accessToken,
    tenantId: defaultTenantId,
    profile: mePayload?.data ?? {},
  });
  await page.goto(`${appBaseUrl}${selectWorkspacePath()}`, { waitUntil: "domcontentloaded" });
  const workspaceRoutePattern = /\/(space|workspace)\/[^/]+\/.+/;
  await expect.poll(async () => {
    if (workspaceRoutePattern.test(page.url())) {
      return "workspace";
    }
    const onSelectPage = await page.getByTestId("coze-select-workspace-page").isVisible().catch(() => false);
    return onSelectPage ? "select" : page.url();
  }, {
    timeout: 90_000,
  }).toMatch(/workspace|select/);

  const onSelectPage = await page.getByTestId("coze-select-workspace-page").isVisible().catch(() => false);
  if (onSelectPage) {
    const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
    if (await matchedWorkspaceButton.count()) {
      await matchedWorkspaceButton.click();
    } else {
      await page.locator('[data-testid^="coze-select-workspace-"]').first().click();
    }
    await page.waitForURL(/\/(space|workspace)\/[^/]+\/.+/, { timeout: 45_000 });
  }
}

async function openParameterCreatePage(page: import("@playwright/test").Page, appKey: string) {
  const workspaceId = readWorkspaceIdFromUrl(page.url());
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  const microflow = await createMicroflow(page, workspaceId);
  await page.goto(`${appBaseUrl}/microflow/${encodeURIComponent(microflow.id)}/editor`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
  return microflow;
}

async function ensureNodePanelVisible(page: import("@playwright/test").Page) {
  const panel = page.getByTestId("microflow-editor-right-node-panel");
  if (await panel.isVisible().catch(() => false)) {
    return panel;
  }
  await page.getByTestId("microflow-node-panel-rail").click();
  await expect(panel).toBeVisible({ timeout: 15_000 });
  return panel;
}

async function ensurePropertiesPanelVisible(page: import("@playwright/test").Page) {
  const propertyPanel = page.getByTestId("microflow-property-panel");
  if (await propertyPanel.isVisible().catch(() => false)) {
    return propertyPanel;
  }
  const expandButton = page.getByTestId("microflow-editor-right-shell").locator('button[title="Properties"]').first();
  await expandButton.evaluate(node => (node as HTMLButtonElement).click());
  await expect(propertyPanel).toBeVisible({ timeout: 15_000 });
  return propertyPanel;
}

async function saveEditor(page: import("@playwright/test").Page) {
  const saveButton = page.getByTestId("microflow-editor-save");
  await expect(saveButton).toBeEnabled({ timeout: 15_000 });
  await saveButton.evaluate(node => (node as HTMLButtonElement).click());
  await page.waitForTimeout(800);
}

async function readSchema(page: import("@playwright/test").Page, microflowId: string) {
  const schemaResp = await page.request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(microflowId)}/schema`, {
    headers: {
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
  });
  expect(schemaResp.ok()).toBeTruthy();
  return schemaResp.json();
}

test.describe.serial("@microflow Mendix Studio Parameter Create", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("可从工具箱创建参数节点并写回 schema.parameters", async ({ page }) => {
    await loginForParameterCreate(page, appKey);
    const microflow = await openParameterCreatePage(page, appKey);

    await ensureNodePanelVisible(page);
    const canvas = page.locator(".microflow-flowgram-canvas");
    await expect(canvas).toBeVisible();
    await page.getByTestId("microflow-node-panel-search").locator("input").fill("Parameter");
    const parameterItem = page.getByTestId("microflow-node-panel-item-parameter").first();
    await expect(parameterItem).toBeVisible({ timeout: 15_000 });
    await parameterItem.dispatchEvent("dblclick");

    const parameterNode = page.locator(".microflow-flowgram-node--category-flow").filter({ hasText: "parameter" }).last();
    await expect(parameterNode).toBeVisible({ timeout: 20_000 });
    const propertyPanel = await ensurePropertiesPanelVisible(page);
    await parameterNode.click({ position: { x: 60, y: 24 } });
    const parameterNameInput = propertyPanel.getByLabel("Parameter Name");
    await expect(parameterNameInput).toBeVisible({ timeout: 15_000 });
    await parameterNameInput.fill("amount");
    await saveEditor(page);

    const schemaPayload = await readSchema(page, microflow.id);
    expect(schemaPayload?.data?.schema?.parameters).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: "amount" }),
    ]));
    const persistedParameterNode = (schemaPayload?.data?.schema?.workflow?.nodes ?? []).find((node: { type?: string; data?: { parameterName?: string } }) =>
      node.type === "parameterObject" && node.data?.parameterName === "amount");
    expect(persistedParameterNode?.id).toBeTruthy();
    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId(`microflow-node-${String(persistedParameterNode?.id)}`)).toBeVisible({ timeout: 20_000 });
  });
});
