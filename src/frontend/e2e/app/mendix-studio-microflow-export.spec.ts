import fs from "node:fs/promises";

import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultPassword, defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

function readWorkspaceIdFromUrl(url: string): string | undefined {
  return url.match(/\/space\/([^/]+)/)?.[1]
    ?? url.match(/\/workspace\/([^/]+)/)?.[1];
}

let authenticatedAccessToken = "";

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string) {
  const name = uniqueName("E2E_MF_EXPORT").replace(/-/g, "_");
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
        tags: ["e2e", "export"],
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

async function loginForExport(page: import("@playwright/test").Page, appKey: string) {
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

async function openExportPage(page: import("@playwright/test").Page, appKey: string) {
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

test.describe.serial("@microflow Mendix Studio Export", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("工具栏可导出当前微流为 PNG 图片", async ({ page }, testInfo) => {
    await loginForExport(page, appKey);
    const microflow = await openExportPage(page, appKey);

    const downloadPromise = page.waitForEvent("download");
    await page.getByTestId("microflow-editor-export-image").evaluate(node => (node as HTMLButtonElement).click());
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe(`${microflow.name}.png`);

    const outputPath = testInfo.outputPath(download.suggestedFilename());
    await download.saveAs(outputPath);
    const stat = await fs.stat(outputPath);
    expect(stat.size).toBeGreaterThan(0);
  });
});
