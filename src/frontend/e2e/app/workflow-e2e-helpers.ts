import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";
import { ensureAppSetup, loginApp, navigateBySidebar } from "./helpers";

export interface WorkflowSessionContext {
  appKey: string;
  workflowId: string;
}

interface CreateWorkflowSessionOptions {
  reuseExisting?: boolean;
}

const workflowCanvasSelector = ".wf-react-canvas-shell";
const workflowNodeSelector = ".gedit-flow-activity-node, .wf-react-node";
const workflowEdgeSelector = ".wf-react-edge-path";
let cachedWorkflowSession: WorkflowSessionContext | null = null;

function randomBetween(min: number, max: number) {
  return min + Math.random() * (max - min);
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

async function moveMouseHumanLike(page: Page, from: { x: number; y: number }, to: { x: number; y: number }, stepsHint = 20) {
  const deltaX = to.x - from.x;
  const deltaY = to.y - from.y;
  const distance = Math.hypot(deltaX, deltaY);
  const steps = Math.max(14, Math.min(32, Math.max(stepsHint, Math.round(distance / 26))));

  await page.mouse.move(from.x, from.y, { steps: 1 });

  for (let step = 1; step <= steps; step += 1) {
    const progress = step / steps;
    const easing = progress < 0.5
      ? 2 * progress * progress
      : 1 - Math.pow(-2 * progress + 2, 2) / 2;
    const jitterScale = Math.max(0.75, (1 - progress) * 4);
    const nextX = to.x === from.x ? to.x : from.x + deltaX * easing + randomBetween(-jitterScale, jitterScale);
    const nextY = to.y === from.y ? to.y : from.y + deltaY * easing + randomBetween(-jitterScale, jitterScale);
    await page.mouse.move(nextX, nextY, { steps: 1 });
    await page.waitForTimeout(randomBetween(8, 20));
  }

  await page.mouse.move(to.x, to.y, { steps: 1 });
}

async function resolveLocatorPoint(
  page: Page,
  locator: Locator,
  position?: { x: number; y: number }
): Promise<{ x: number; y: number }> {
  await expect(locator).toBeVisible({ timeout: 15_000 });
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  expect(box).toBeTruthy();
  if (!box) {
    throw new Error("目标元素未能解析出可交互区域。");
  }

  const x = clamp(
    box.x + (position?.x ?? box.width / 2),
    box.x + 1,
    box.x + Math.max(1, box.width - 1)
  );
  const y = clamp(
    box.y + (position?.y ?? box.height / 2),
    box.y + 1,
    box.y + Math.max(1, box.height - 1)
  );

  await page.mouse.move(x, y, { steps: 1 });
  return { x, y };
}

async function ensureWorkflowListReady(
  page: Page,
  appKey: string,
  ensureLoggedInSession?: (appKey: string) => Promise<void>
): Promise<void> {
  const workflowsRegex = new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows(?:\\?.*)?$`);
  const loginRegex = new RegExp(`/apps/${encodeURIComponent(appKey)}/login(?:\\?.*)?$`);
  const createButton = page.getByTestId("app-workflows-create");

  for (let attempt = 0; attempt < 3; attempt += 1) {
    if (loginRegex.test(page.url()) || (await page.getByTestId("app-login-page").isVisible().catch(() => false))) {
      if (ensureLoggedInSession) {
        await ensureLoggedInSession(appKey);
      } else {
        await loginApp(page, appKey);
      }
    }

    try {
      await navigateBySidebar(page, "workflows", {
        pageTestId: "app-workflows-page",
        urlPattern: workflowsRegex
      });
      await expect(createButton).toBeVisible({ timeout: 8_000 });
      return;
    } catch {
      if (ensureLoggedInSession) {
        await ensureLoggedInSession(appKey);
      }
      if (attempt === 2) {
        throw new Error(`工作流列表页未稳定进入可操作状态，当前 URL: ${page.url()}`);
      }
    }
  }
}

export async function expectWorkflowEditorReady(page: Page): Promise<void> {
  await expect(page.locator(workflowCanvasSelector)).toBeVisible({ timeout: 30_000 });
  await expect(page.locator(workflowNodeSelector).first()).toBeVisible({ timeout: 15_000 });
}

export function workflowNodeLocator(page: Page) {
  return page.locator(workflowNodeSelector);
}

export function workflowConnectionLocator(page: Page) {
  return page.locator(workflowEdgeSelector);
}

export function workflowCanvasLocator(page: Page) {
  return page.locator(workflowCanvasSelector);
}

export async function clickWorkflowTestRun(
  page: Page,
  inputJson = "{\"input\":\"hello\"}"
): Promise<void> {
  await page.getByTestId("workflow.detail.toolbar.test-run").click();
  const panel = page.getByTestId("workflow.detail.node.testrun.result-panel");
  await expect(panel).toBeVisible({ timeout: 15_000 });

  const selects = panel.locator(".ant-select");
  const sourceSelect = selects.nth(1);
  if (await sourceSelect.isVisible().catch(() => false)) {
    const currentSourceLabel = (await sourceSelect.textContent().catch(() => "")) ?? "";
    if (!/草稿版本|Draft/i.test(currentSourceLabel)) {
      await sourceSelect.click();
      await page.keyboard.press("ArrowDown");
      await page.waitForTimeout(120);
      await page.keyboard.press("Enter");
      await page.waitForTimeout(180);
    }
  }

  await panel.locator("textarea").first().fill(inputJson);
  await panel.locator(".ant-btn-primary").first().click();
}

export async function hoverCanvasAt(page: Page, offset: { x: number; y: number }): Promise<void> {
  const canvas = workflowCanvasLocator(page);
  await expect(canvas).toBeVisible({ timeout: 15_000 });
  const box = await canvas.boundingBox();
  expect(box).toBeTruthy();
  if (!box) {
    throw new Error("工作流画布未能获取定位区域。");
  }

  const target = {
    x: clamp(box.x + offset.x, box.x + 6, box.x + Math.max(6, box.width - 6)),
    y: clamp(box.y + offset.y, box.y + 6, box.y + Math.max(6, box.height - 6))
  };
  const start = {
    x: clamp(box.x + Math.min(32, box.width * 0.08), box.x + 4, box.x + Math.max(4, box.width - 4)),
    y: clamp(box.y + Math.min(32, box.height * 0.08), box.y + 4, box.y + Math.max(4, box.height - 4))
  };

  await moveMouseHumanLike(page, start, target);
  await page.waitForTimeout(randomBetween(18, 42));
}

export async function humanHoverLocator(
  page: Page,
  locator: Locator,
  position?: { x: number; y: number }
): Promise<void> {
  const target = await resolveLocatorPoint(page, locator, position);
  const start = {
    x: Math.max(12, target.x - randomBetween(90, 160)),
    y: Math.max(12, target.y - randomBetween(24, 72))
  };
  await moveMouseHumanLike(page, start, target);
  await page.waitForTimeout(randomBetween(20, 48));
}

export async function dragNodeCatalogItemToCanvas(
  page: Page,
  keyword: string,
  canvasOffset: { x: number; y: number }
): Promise<void> {
  const panel = page.getByTestId("workflow.detail.node-panel");
  await expect(panel).toBeVisible({ timeout: 15_000 });
  const searchInput = page.getByTestId("workflow.detail.node-panel.search");
  await searchInput.fill(keyword);

  const nodeItem = page.locator(".wf-react-node-item").first();
  await expect(nodeItem).toBeVisible({ timeout: 15_000 });
  const source = await resolveLocatorPoint(page, nodeItem);

  const canvas = workflowCanvasLocator(page);
  await expect(canvas).toBeVisible({ timeout: 15_000 });
  const canvasBox = await canvas.boundingBox();
  expect(canvasBox).toBeTruthy();
  if (!canvasBox) {
    throw new Error("工作流画布不可用，无法执行拖拽。");
  }

  const target = {
    x: clamp(canvasBox.x + canvasOffset.x, canvasBox.x + 16, canvasBox.x + Math.max(16, canvasBox.width - 16)),
    y: clamp(canvasBox.y + canvasOffset.y, canvasBox.y + 16, canvasBox.y + Math.max(16, canvasBox.height - 16))
  };

  await moveMouseHumanLike(page, { x: source.x - randomBetween(60, 110), y: source.y - randomBetween(10, 34) }, source);
  await page.waitForTimeout(randomBetween(18, 42));
  await page.mouse.down();
  await page.waitForTimeout(randomBetween(42, 88));

  const dragTrigger = {
    x: source.x + randomBetween(14, 24),
    y: source.y + randomBetween(8, 18)
  };
  await moveMouseHumanLike(page, source, dragTrigger, 8);
  await page.waitForTimeout(randomBetween(28, 56));
  await moveMouseHumanLike(page, dragTrigger, target, 24);
  await page.waitForTimeout(randomBetween(36, 72));
  await page.mouse.up();

  await expect(panel).toBeHidden({ timeout: 15_000 });
}

export async function connectWorkflowPorts(
  page: Page,
  sourcePort: Locator,
  targetPort: Locator
): Promise<void> {
  const source = await resolveLocatorPoint(page, sourcePort);
  const target = await resolveLocatorPoint(page, targetPort);

  await moveMouseHumanLike(page, { x: source.x - randomBetween(70, 120), y: source.y - randomBetween(12, 40) }, source);
  await page.waitForTimeout(randomBetween(16, 34));
  await page.mouse.down();
  await page.waitForTimeout(randomBetween(28, 60));
  await moveMouseHumanLike(
    page,
    source,
    {
      x: source.x + randomBetween(18, 28),
      y: source.y + randomBetween(-4, 4)
    },
    8
  );
  await page.waitForTimeout(randomBetween(18, 44));
  await moveMouseHumanLike(page, source, target, 22);
  await page.waitForTimeout(randomBetween(18, 44));
  await page.mouse.up();
}

export async function loginToWorkflowList(
  page: Page,
  request: APIRequestContext,
  ensureLoggedInSession?: (appKey: string) => Promise<void>
): Promise<string> {
  const appKey = await ensureAppSetup(request);
  if (ensureLoggedInSession) {
    await ensureLoggedInSession(appKey);
  }
  await ensureWorkflowListReady(page, appKey, ensureLoggedInSession);
  return appKey;
}

export async function createWorkflowAndOpenEditor(page: Page, appKey: string): Promise<string> {
  const createResponsePromise = page.waitForResponse((response) => {
    return response.request().method() === "POST" && /\/api\/v2\/workflows$/.test(response.url());
  });

  await page.getByTestId("app-workflows-create").click();
  const createResponse = await createResponsePromise;
  expect(createResponse.ok()).toBeTruthy();

  const createPayload = (await createResponse.json()) as { data?: { id?: string } | string };
  const createdWorkflowId =
    typeof createPayload.data === "string"
      ? createPayload.data
      : (createPayload.data?.id ?? "");
  expect(createdWorkflowId).not.toBe("");

  await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/[^/]+/editor(?:\\?.*)?$`), {
    timeout: 30_000
  });
  await expectWorkflowEditorReady(page);
  return createdWorkflowId;
}

export async function createWorkflowSession(
  page: Page,
  request: APIRequestContext,
  ensureLoggedInSession?: (appKey: string) => Promise<void>,
  options?: CreateWorkflowSessionOptions
): Promise<WorkflowSessionContext> {
  const reuseExisting = options?.reuseExisting ?? true;
  if (reuseExisting && cachedWorkflowSession) {
    try {
      await openWorkflowEditor(page, cachedWorkflowSession.appKey, cachedWorkflowSession.workflowId);
      return cachedWorkflowSession;
    } catch {
      cachedWorkflowSession = null;
    }
  }

  const appKey = await loginToWorkflowList(page, request, ensureLoggedInSession);
  const workflowId = await createWorkflowAndOpenEditor(page, appKey);
  const session = { appKey, workflowId };
  if (reuseExisting) {
    cachedWorkflowSession = session;
  }
  return session;
}

export async function openWorkflowEditor(page: Page, appKey: string, workflowId: string): Promise<void> {
  await ensureWorkflowListReady(page, appKey);
  const row = page.locator(`tr[data-row-key="${workflowId}"]`).first();
  await expect(row).toBeVisible({ timeout: 30_000 });
  await row.getByTestId(`app-workflows-open-${workflowId}`).click();
  await page.waitForURL(
    new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(workflowId)}/editor(?:\\?.*)?$`),
    {
      timeout: 30_000
    }
  );
  await expectWorkflowEditorReady(page);
}
