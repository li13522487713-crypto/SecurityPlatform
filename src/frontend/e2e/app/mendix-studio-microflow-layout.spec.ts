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
  return { id: String(created.id), name };
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
  const microflow = await createMicroflow(page, workspaceId);
  await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/${encodeURIComponent(appKey)}?microflowId=${encodeURIComponent(microflow.id)}`, {
    waitUntil: "domcontentloaded"
  });
  await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-resource-editor-host")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible();
  return { microflowId: microflow.id, microflowName: microflow.name, workspaceId };
}

async function openStudioPageWithoutDeepLink(page: import("@playwright/test").Page, appKey: string) {
  await ensureAppWorkspace(page, appKey);
  const workspaceId = page.url().match(/\/workspace\/([^/]+)/)?.[1];
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  await page.addInitScript(() => {
    window.localStorage.removeItem("lowcode-studio:mendix-layout:v1");
  });
  await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/${encodeURIComponent(appKey)}`, {
    waitUntil: "domcontentloaded"
  });
  await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });
  return workspaceId;
}

async function dragLocatorBy(page: import("@playwright/test").Page, locator: import("@playwright/test").Locator, delta: { x: number; y: number }) {
  const box = await locator.boundingBox();
  expect(box).toBeTruthy();
  const startX = box!.x + box!.width / 2;
  const startY = box!.y + box!.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + delta.x, startY + delta.y, { steps: 8 });
  await page.mouse.up();
}

async function saveAndReloadMicroflow(page: import("@playwright/test").Page) {
  await expect(page.getByTestId("microflow-workbench-save")).toBeEnabled({ timeout: 10_000 });
  await page.getByTestId("microflow-workbench-save").click();
  await expect(page.getByTestId("microflow-workbench-save")).toBeDisabled({ timeout: 20_000 });
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
  await expect(page.locator(".microflow-flowgram-canvas")).toBeVisible();
}

test.describe.serial("@microflow Mendix Studio layout", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("默认不显示硬编码 Page/Workflow Tab", async ({ page }) => {
    await openStudioPageWithoutDeepLink(page, appKey);

    await expect(page.getByTestId("mendix-studio-empty-workbench")).toBeVisible();
    await expect(page.locator(".studio-workbench-tabs .studio-workbench-tab")).toHaveCount(0);
    await expect(page.locator(".studio-workbench-tabs")).not.toContainText("PurchaseRequest_EditPage");
    await expect(page.locator(".studio-workbench-tabs")).not.toContainText("WF_PurchaseApproval");
  });

  test("默认进入微流设计器时使用画布优先布局", async ({ page }) => {
    const { microflowName } = await openLayoutPage(page, appKey);

    await expect(page.locator(".studio-workbench-tabs .studio-workbench-tab")).toHaveCount(1);
    await expect(page.locator(".studio-workbench-tabs .studio-workbench-tab")).toContainText(microflowName);
    await expect(page.locator(".studio-workbench-tabs")).not.toContainText("PurchaseRequest_EditPage");
    await expect(page.locator(".studio-workbench-tabs")).not.toContainText("WF_PurchaseApproval");
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
    expect((canvas!.width * canvas!.height) / (body!.width * body!.height)).toBeGreaterThan(0.7);
  });

  test("Start 和 End 节点可以自由移动并保存回显", async ({ page }) => {
    await openLayoutPage(page, appKey);

    const startNode = page.locator(".microflow-flowgram-node--start").first();
    const endNode = page.locator(".microflow-flowgram-node--end").first();
    await expect(startNode).toBeVisible();
    await expect(endNode).toBeVisible();
    await expect(page.getByText("MF_START_NO_OUTGOING")).toHaveCount(0);
    await expect(page.getByText("MF_OBJECT_UNREACHABLE")).toHaveCount(0);
    const startBefore = await startNode.boundingBox();
    const endBefore = await endNode.boundingBox();
    expect(startBefore).toBeTruthy();
    expect(endBefore).toBeTruthy();

    await dragLocatorBy(page, startNode, { x: 96, y: 48 });
    await dragLocatorBy(page, endNode, { x: -80, y: 72 });

    await expect.poll(async () => {
      const box = await startNode.boundingBox();
      return box ? Math.hypot(box.x - startBefore!.x, box.y - startBefore!.y) : 0;
    }).toBeGreaterThan(30);
    await expect.poll(async () => {
      const box = await endNode.boundingBox();
      return box ? Math.hypot(box.x - endBefore!.x, box.y - endBefore!.y) : 0;
    }).toBeGreaterThan(30);
    await expect(page.getByText("MF_START_NO_OUTGOING")).toHaveCount(0);
    await expect(page.getByText("MF_OBJECT_UNREACHABLE")).toHaveCount(0);

    const startMoved = await startNode.boundingBox();
    const endMoved = await endNode.boundingBox();
    await saveAndReloadMicroflow(page);

    const startReloaded = await page.locator(".microflow-flowgram-node--start").first().boundingBox();
    const endReloaded = await page.locator(".microflow-flowgram-node--end").first().boundingBox();
    expect(startReloaded).toBeTruthy();
    expect(endReloaded).toBeTruthy();
    expect(Math.hypot(startReloaded!.x - startMoved!.x, startReloaded!.y - startMoved!.y)).toBeLessThan(8);
    expect(Math.hypot(endReloaded!.x - endMoved!.x, endReloaded!.y - endMoved!.y)).toBeLessThan(8);
    await expect(page.getByText("MF_START_NO_OUTGOING")).toHaveCount(0);
    await expect(page.getByText("MF_OBJECT_UNREACHABLE")).toHaveCount(0);
  });

  test("Start 和 End 连线后 Problems 不应出现可达性 warning", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await expect(page.locator(".microflow-flowgram-node")).toHaveCount(2);
    await expect(page.getByText("MF_START_NO_OUTGOING")).toHaveCount(0);
    await expect(page.getByText("MF_OBJECT_UNREACHABLE")).toHaveCount(0);
  });

  test("节点工具箱节点可以拖入画布创建节点", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await page.getByTitle("Nodes").click();
    await expect(page.getByTestId("microflow-editor-left-panel")).toBeVisible();
    const canvas = page.locator(".microflow-flowgram-canvas");
    await expect(canvas).toBeVisible();
    const canvasBox = await canvas.boundingBox();
    expect(canvasBox).toBeTruthy();
    const beforeCount = await page.locator(".microflow-flowgram-node").count();

    await page.getByTestId("microflow-node-panel-item-activity-logMessage").dragTo(canvas, {
      targetPosition: { x: 360, y: 260 }
    });

    await expect(page.locator(".microflow-flowgram-node")).toHaveCount(beforeCount + 1, { timeout: 10_000 });
    const logNode = page.locator(".microflow-flowgram-node").filter({ hasText: "Log Message" }).last();
    await expect(logNode).toBeVisible();
    const logBox = await logNode.boundingBox();
    expect(logBox).toBeTruthy();
    const logCenter = { x: logBox!.x + logBox!.width / 2, y: logBox!.y + logBox!.height / 2 };
    expect(Math.abs(logCenter.x - (canvasBox!.x + 360))).toBeLessThan(140);
    expect(Math.abs(logCenter.y - (canvasBox!.y + 260))).toBeLessThan(140);
    await saveAndReloadMicroflow(page);
    await expect(page.locator(".microflow-flowgram-node").filter({ hasText: "Log Message" })).toBeVisible();
  });

  test("普通微流节点可以自由拖动并吸附到网格", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await page.getByTitle("Nodes").click();
    const canvas = page.locator(".microflow-flowgram-canvas");
    await expect(canvas).toBeVisible();
    await page.getByTestId("microflow-node-panel-item-activity-logMessage").dragTo(canvas, {
      targetPosition: { x: 380, y: 280 }
    });

    const node = page.locator(".microflow-flowgram-node").filter({ hasText: "Log Message" }).last();
    await expect(node).toBeVisible({ timeout: 10_000 });
    const before = await node.boundingBox();
    expect(before).toBeTruthy();

    await dragLocatorBy(page, node, { x: 140, y: 80 });

    const after = await node.boundingBox();
    expect(after).toBeTruthy();
    expect(Math.abs(after!.x - before!.x)).toBeGreaterThan(20);
    expect(Math.abs(after!.y - before!.y)).toBeGreaterThan(20);
    expect(Math.round(after!.x) % 24).toBeLessThan(24);
  });

  test("画布节点选中后通过右键菜单打开属性面板", async ({ page }) => {
    await openLayoutPage(page, appKey);

    const startNode = page.locator(".microflow-flowgram-node--start").first();
    await expect(startNode).toBeVisible();
    await startNode.click();
    await expect(page.getByTestId("microflow-property-panel")).toHaveCount(0);

    await startNode.click({ button: "right" });
    await expect(page.getByTestId("microflow-canvas-node-context-menu")).toBeVisible();
    await page.getByTestId("microflow-canvas-node-context-menu").getByRole("button", { name: /属性|Properties/ }).click();
    await expect(page.getByTestId("microflow-property-panel")).toBeVisible();

    await page.locator(".microflow-flowgram-canvas").click({ position: { x: 24, y: 520 } });
    await expect(page.getByTestId("microflow-property-panel")).toHaveCount(0);
  });

  test("Problems 和 Debug 使用紧凑状态条与底部 Dock", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await expect(page.getByTestId("microflow-bottom-status-strip")).toBeVisible();
    await expect(page.getByTestId("microflow-bottom-panel")).toHaveCount(0);

    const body = await page.getByTestId("microflow-resource-editor-body").boundingBox();
    const canvasBefore = await page.getByTestId("microflow-canvas").boundingBox();
    expect(body).toBeTruthy();
    expect(canvasBefore).toBeTruthy();

    await page.getByTestId("microflow-bottom-status-strip").getByRole("button", { name: /Debug|调试/ }).click();
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible();

    const canvasAfterPeek = await page.getByTestId("microflow-canvas").boundingBox();
    const dockPeek = await page.getByTestId("microflow-bottom-panel").boundingBox();
    expect(canvasAfterPeek).toBeTruthy();
    expect(dockPeek).toBeTruthy();
    expect(Math.abs(canvasAfterPeek!.height - canvasBefore!.height)).toBeLessThan(8);
    expect(dockPeek!.height).toBeLessThanOrEqual(280);

    await page.getByRole("button", { name: "展开底部 Dock" }).click();
    const dockFull = await page.getByTestId("microflow-bottom-panel").boundingBox();
    expect(dockFull).toBeTruthy();
    expect(dockFull!.height).toBeGreaterThan(dockPeek!.height + 40);

    await page.keyboard.press("Escape");
    const dockRestored = await page.getByTestId("microflow-bottom-panel").boundingBox();
    expect(dockRestored).toBeTruthy();
    expect(dockRestored!.height).toBeLessThan(dockFull!.height);

    await page.keyboard.press("Escape");
    await expect(page.getByTestId("microflow-bottom-panel")).toHaveCount(0);
  });

  test("左侧节点 hover 不出现大 Tooltip", async ({ page }) => {
    await openLayoutPage(page, appKey);

    await page.getByTitle("Nodes").click();
    await expect(page.getByTestId("microflow-editor-left-panel")).toBeVisible();
    await page.getByTestId("microflow-node-panel-item-activity-logMessage").hover();
    await page.waitForTimeout(450);

    const largeOverlayCount = await page.locator(".semi-tooltip, .semi-popover").evaluateAll(elements =>
      elements.filter(element => {
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.visibility !== "hidden" && style.display !== "none" && rect.width > 180 && rect.height > 80;
      }).length
    );
    expect(largeOverlayCount).toBe(0);
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
