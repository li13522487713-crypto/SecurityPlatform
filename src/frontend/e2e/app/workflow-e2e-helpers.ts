import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { ensureAppSetup, loginApp, navigateBySidebar } from "./helpers";

export interface WorkflowSessionContext {
  appKey: string;
  workflowId: string;
}

const workflowCanvasSelector = ".wf-react-canvas-shell";
const workflowNodeSelector = ".gedit-flow-activity-node, .wf-react-node";

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
  ensureLoggedInSession?: (appKey: string) => Promise<void>
): Promise<WorkflowSessionContext> {
  const appKey = await loginToWorkflowList(page, request, ensureLoggedInSession);
  const workflowId = await createWorkflowAndOpenEditor(page, appKey);
  return { appKey, workflowId };
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
