import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { appBaseUrl, clearAuthStorage, ensureAppSetup, loginApp } from "./helpers";

export interface WorkflowSessionContext {
  appKey: string;
  workflowId: string;
}

export async function loginToWorkflowList(page: Page, request: APIRequestContext): Promise<string> {
  const appKey = await ensureAppSetup(request);
  await clearAuthStorage(page);
  await loginApp(page, appKey);
  await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows`);
  await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows(?:\\?.*)?$`), { timeout: 30_000 });
  return appKey;
}

export async function createWorkflowAndOpenEditor(page: Page, appKey: string): Promise<string> {
  const createResponsePromise = page.waitForResponse((response) => {
    return response.request().method() === "POST" && /\/api\/v2\/workflows$/.test(response.url());
  });

  await page.getByRole("button", { name: /新建工作流|Create Workflow/ }).click();
  const createResponse = await createResponsePromise;
  expect(createResponse.ok()).toBeTruthy();

  const createPayload = (await createResponse.json()) as { data?: { id?: string } | string };
  const createdWorkflowId =
    typeof createPayload.data === "string"
      ? createPayload.data
      : (createPayload.data?.id ?? "");
  expect(createdWorkflowId).not.toBe("");

  await page.waitForTimeout(500);
  if (!/\/workflows\/[^/]+\/editor$/.test(page.url())) {
    await page.goto(
      `${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(createdWorkflowId)}/editor`
    );
  }

  await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/[^/]+/editor(?:\\?.*)?$`), {
    timeout: 30_000
  });
  await expect(page.locator(".wf-react-canvas-shell")).toBeVisible();
  return createdWorkflowId;
}

export async function createWorkflowSession(
  page: Page,
  request: APIRequestContext
): Promise<WorkflowSessionContext> {
  const appKey = await loginToWorkflowList(page, request);
  const workflowId = await createWorkflowAndOpenEditor(page, appKey);
  return { appKey, workflowId };
}

export async function openWorkflowEditor(page: Page, appKey: string, workflowId: string): Promise<void> {
  await page.goto(
    `${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(workflowId)}/editor`
  );
  await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(workflowId)}/editor(?:\\?.*)?$`), {
    timeout: 30_000
  });
  await expect(page.locator(".wf-react-canvas-shell")).toBeVisible();
}
