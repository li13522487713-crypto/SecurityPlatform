import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultTenantId, ensureAppSetup, ensureAppWorkspace, uniqueName } from "./helpers";

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string) {
  const name = uniqueName("E2E_MF_LAYOUT").replace(/-/g, "_");
  const response = await page.request.post(`${appApiBase}/api/v1/microflows`, {
    headers: { "Content-Type": "application/json", "X-Tenant-Id": defaultTenantId },
    data: {
      workspaceId,
      input: {
        name,
        displayName: name,
        moduleId: "Sales",
        moduleName: "Sales",
        tags: ["e2e", "layout"],
        parameters: [],
        returnType: { kind: "void" },
        template: "blank"
      }
    }
  });
  expect(response.ok()).toBeTruthy();

  const listResponse = await page.request.get(`${appApiBase}/api/v1/microflows?workspaceId=${encodeURIComponent(workspaceId)}`, {
    headers: { "X-Tenant-Id": defaultTenantId }
  });
  expect(listResponse.ok()).toBeTruthy();
  const listJson = await listResponse.json();
  const created = (listJson?.data?.items ?? []).find((item: { name?: string; id?: string }) => item.name === name);
  expect(created?.id).toBeTruthy();
  return String(created.id);
}

async function openLayoutPage(page: import("@playwright/test").Page, appKey: string) {
  await ensureAppWorkspace(page, appKey);
  const workspaceId = page.url().match(/\/workspace\/([^/]+)/)?.[1];
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  await page.addInitScript(() => {
    window.localStorage.removeItem("lowcode-studio:mendix-layout:v1");
    window.localStorage.removeItem("atlas_microflow_panel_left_open");
    window.localStorage.removeItem("atlas_microflow_panel_right_open");
    window.localStorage.removeItem("atlas_microflow_panel_bottom_open");
  });
  const microflowId = await createMicroflow(page, workspaceId);
  await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/${encodeURIComponent(appKey)}?microflowId=${encodeURIComponent(microflowId)}`, {
    waitUntil: "domcontentloaded"
  });
  await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-resource-editor-host")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible();
  return { microflowId, workspaceId };
}

test.describe.serial("@microflow Mendix Studio layout", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("默认进入微流设计器时使用画布优先布局", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await expect(page.getByTestId("mendix-studio-app-explorer")).toHaveAttribute("data-collapsed", "false");
    await expect(page.getByTestId("microflow-editor-left-panel")).toHaveCount(0);
    await expect(page.getByTestId("microflow-property-panel")).toHaveCount(0);
    await expect(page.getByTestId("microflow-bottom-panel")).toHaveCount(0);
    await expect(page.getByTestId("microflow-workbench-toolbar")).toBeVisible();
    await expect(page.getByTestId("microflow-editor-toolbar")).toHaveCount(0);

    const body = await page.getByTestId("microflow-resource-editor-body").boundingBox();
    const canvas = await page.getByTestId("microflow-canvas").boundingBox();
    expect(body).toBeTruthy();
    expect(canvas).toBeTruthy();
    expect((canvas!.width * canvas!.height) / (body!.width * body!.height)).toBeGreaterThan(0.65);
  });

  test("布局状态可以持久化并通过 F11 进入专注模式", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await page.getByTitle("Nodes").click();
    await expect(page.getByTestId("microflow-editor-left-panel")).toBeVisible();
    await page.keyboard.press("F11");
    await expect(page.getByTestId("microflow-editor-left-panel")).toHaveCount(0);
    await expect(page.getByTestId("microflow-bottom-panel")).toHaveCount(0);

    await page.keyboard.press("F11");
    await expect(page.getByTestId("microflow-editor-left-panel")).toBeVisible();
    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("microflow-editor-left-panel")).toBeVisible();
  });
});
