import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";
import { signPath } from "@atlas/app-shell-shared";
import {
  clamp,
  humanDrag,
  moveMouseHumanLike,
  randomBetween,
  resolveLocatorPoint
} from "../fixtures/human-mouse";
import { clickCrudSubmit, ensureAppSetup, loginApp, navigateBySidebar, uniqueName } from "./helpers";

export interface WorkflowSessionContext {
  appKey: string;
  workflowId: string;
}

interface CreateWorkflowSessionOptions {
  reuseExisting?: boolean;
}

const workflowCanvasSelector = '[data-testid="app-workflow-editor-shell"], [data-testid="app-chatflow-editor-shell"]';
const workflowNodeSelector = ".module-workflow__node-card";
const workflowEdgeSelector = ".wf-react-edge-path";
let cachedWorkflowSession: WorkflowSessionContext | null = null;

async function ensureWorkflowListReady(
  page: Page,
  appKey: string,
  ensureLoggedInSession?: (appKey: string) => Promise<void>
): Promise<void> {
  const loginRegex = new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`);

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
        pageTestId: "app-workflows-page"
      });
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
  await expect(page.getByTestId("workflow.detail.title.save-draft")).toBeVisible({ timeout: 15_000 });
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
  await page.getByTestId("workflow.detail.run-inputs").fill(inputJson);
  await page.getByTestId("workflow.detail.toolbar.test-run").click();
  const panel = page.getByTestId("workflow.detail.node.testrun.result-panel");
  await expect(panel).toBeVisible({ timeout: 15_000 });
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

  await moveMouseHumanLike(page, target, { targetWidth: Math.min(box.width, box.height) });
  await page.waitForTimeout(randomBetween(18, 42));
}

export async function humanHoverLocator(
  page: Page,
  locator: Locator,
  position?: { x: number; y: number }
): Promise<void> {
  const target = await resolveLocatorPoint(page, locator, position);
  await moveMouseHumanLike(page, target, { targetWidth: 24 });
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

  await humanDrag(page, source, target, {
    stepsHint: 24,
    gripDelay: { min: 42, max: 88 },
    hesitateNearTarget: true
  });

  await expect(panel).toBeHidden({ timeout: 15_000 });
}

export async function connectWorkflowPorts(
  page: Page,
  sourcePort: Locator,
  targetPort: Locator
): Promise<void> {
  const source = await resolveLocatorPoint(page, sourcePort);
  const target = await resolveLocatorPoint(page, targetPort);

  await humanDrag(page, source, target, {
    stepsHint: 22,
    gripDelay: { min: 28, max: 60 },
    hesitateNearTarget: true
  });
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
  const currentUrl = new URL(page.url());
  currentUrl.searchParams.set("create", "1");
  await page.goto(currentUrl.toString());
  const dialog = page.locator(".semi-modal-content").last();
  await expect(dialog).toBeVisible({ timeout: 15_000 });
  await dialog.getByRole("textbox").nth(1).fill(uniqueName("E2EWorkflow"));
  const createResponsePromise = page.waitForResponse((response) => {
    return response.request().method() === "POST" && /\/api\/v2\/workflows$/.test(response.url());
  });
  await clickCrudSubmit(page);
  const createResponse = await createResponsePromise;
  expect(createResponse.ok()).toBeTruthy();

  const createPayload = (await createResponse.json()) as { data?: { id?: string } | string };
  const createdWorkflowId =
    typeof createPayload.data === "string"
      ? createPayload.data
      : (createPayload.data?.id ?? "");
  expect(createdWorkflowId).not.toBe("");

  await page.waitForURL(new RegExp(`/org/[^/]+/workspaces/[^/]+/workflows/[^/?]+(?:\\?.*)?$`), {
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
  const reuseExisting = options?.reuseExisting ?? false;
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
  void appKey;
  await ensureWorkflowListReady(page, appKey);
  const currentUrl = new URL(page.url());
  const nextUrl = `${currentUrl.origin}${currentUrl.pathname.replace(/\/workflows(?:\/[^/?]+)?$/, `/workflows/${encodeURIComponent(workflowId)}`)}${currentUrl.search}`;
  await page.goto(nextUrl);
  await page.waitForURL(new RegExp(`/org/[^/]+/workspaces/[^/]+/workflows/${encodeURIComponent(workflowId)}(?:\\?.*)?$`), {
    timeout: 30_000
  });
  await expectWorkflowEditorReady(page);
}
