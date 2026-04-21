import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";
import { signPath } from "@atlas/app-shell-shared";
import {
  clamp,
  humanDrag,
  moveMouseHumanLike,
  randomBetween,
  resolveLocatorPoint
} from "../fixtures/human-mouse";
import { ensureAppSetup, loginApp, navigateBySidebar, uniqueName } from "./helpers";
import {
  appApiBase,
  defaultPassword,
  defaultTenantId,
  defaultUsername
} from "./helpers";

export interface WorkflowSessionContext {
  appKey: string;
  workflowId: string;
}

interface CreateWorkflowSessionOptions {
  reuseExisting?: boolean;
}

const workflowCanvasSelector = [
  '[data-testid="app-workflow-editor-shell"]',
  '[data-testid="app-chatflow-editor-shell"]',
  "#workflow-playground-content",
  ".gedit-playground-container"
].join(", ");
const workflowNodeSelector = ".module-workflow__node-card";
const workflowEdgeSelector = ".wf-react-edge-path";
let cachedWorkflowSession: WorkflowSessionContext | null = null;
let cachedAccessToken: string | null = null;

interface WorkflowCreateResponse {
  success?: boolean;
  code?: string | number;
  message?: string;
  data?: {
    workflow_id?: string | number;
    id?: string | number;
    Id?: string | number;
  };
}

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
        pageTestId: "coze-resource-page",
        urlPattern: /\/workspace\/[^/]+\/resources\/workflows(?:\?.*)?$/
      });
      return;
    } catch {
      if (ensureLoggedInSession) {
        await ensureLoggedInSession(appKey);
      }
      if (attempt === 2) {
        throw new Error(`宸ヤ綔娴佸垪琛ㄩ〉鏈ǔ瀹氳繘鍏ュ彲鎿嶄綔鐘舵€侊紝褰撳墠 URL: ${page.url()}`);
      }
    }
  }
}

async function getAppAccessToken(request: APIRequestContext): Promise<string> {
  if (cachedAccessToken) {
    return cachedAccessToken;
  }

  const response = await request.post(`${appApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });
  expect(response.ok()).toBeTruthy();
  const payload = (await response.json()) as { data?: { accessToken?: string } };
  const token = String(payload.data?.accessToken ?? "").trim();
  expect(token).not.toBe("");
  cachedAccessToken = token;
  return token;
}

function parseWorkspaceIdFromUrl(url: string): string {
  const pathname = new URL(url).pathname;
  const match = pathname.match(/^\/workspace\/([^/]+)\/resources\/workflows(?:\/|$)/);
  expect(match).toBeTruthy();
  return decodeURIComponent((match as RegExpMatchArray)[1]);
}

async function createWorkflowViaApi(
  request: APIRequestContext,
  workspaceId: string,
  name: string
): Promise<string> {
  const accessToken = await getAppAccessToken(request);
  const response = await request.post(`${appApiBase}/api/workflow_api/create`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      name,
      desc: "E2E workflow bootstrap",
      icon_uri: "",
      space_id: workspaceId,
      flow_mode: 0
    }
  });
  expect(response.ok()).toBeTruthy();
  const payload = (await response.json()) as WorkflowCreateResponse;
  const workflowId = String(
    payload.data?.workflow_id ??
    payload.data?.id ??
    payload.data?.Id ??
    ""
  ).trim();
  expect(workflowId).not.toBe("");
  return workflowId;
}

export async function expectWorkflowEditorReady(page: Page): Promise<void> {
  await expect(page.locator(workflowCanvasSelector).first()).toBeVisible({ timeout: 30_000 });

  const saveDraftButton = page.getByTestId("workflow.detail.title.save-draft");
  if ((await saveDraftButton.count()) > 0) {
    await expect(saveDraftButton).toBeVisible({ timeout: 15_000 });
    return;
  }

  await expect(
    page.locator("button").filter({ hasText: /发布|Publish|Variable \(Debug\)/ }).first()
  ).toBeVisible({ timeout: 15_000 });
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
    throw new Error("Workflow canvas bounds are unavailable.");
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
    throw new Error("Workflow canvas is unavailable for drag action.");
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

export async function createWorkflowAndOpenEditor(
  page: Page,
  appKey: string,
  request?: APIRequestContext
): Promise<string> {
  await ensureWorkflowListReady(page, appKey);
  const workspaceId = parseWorkspaceIdFromUrl(page.url());
  if (!request) {
    throw new Error("createWorkflowAndOpenEditor 缺少 request 上下文，无法通过 API 创建 workflow。");
  }
  const createdWorkflowId = await createWorkflowViaApi(request, workspaceId, uniqueName("E2EWorkflow"));
  await page.goto(`/workflow/${encodeURIComponent(createdWorkflowId)}/editor`);
  await page.waitForURL(new RegExp(`/workflow/${encodeURIComponent(createdWorkflowId)}/editor(?:\\?.*)?$`), {
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
  const workflowId = await createWorkflowAndOpenEditor(page, appKey, request);
  const session = { appKey, workflowId };
  if (reuseExisting) {
    cachedWorkflowSession = session;
  }
  return session;
}

export async function openWorkflowEditor(page: Page, appKey: string, workflowId: string): Promise<void> {
  void appKey;
  await page.goto(`/workflow/${encodeURIComponent(workflowId)}/editor`);
  await page.waitForURL(new RegExp(`/workflow/${encodeURIComponent(workflowId)}/editor(?:\\?.*)?$`), {
    timeout: 30_000
  });
  await expectWorkflowEditorReady(page);
}

