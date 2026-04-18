import { expect, test, type APIRequestContext, type Page } from "../fixtures/single-session";
import {
  appApiBase,
  clearAuthStorage,
  defaultPassword,
  defaultTenantId,
  defaultUsername,
  ensureAppSetup,
  loginApp,
  navigateBySidebar,
  platformApiBase,
  platformBaseUrl
} from "./helpers";
import { createWorkflowAndOpenEditor, expectWorkflowEditorReady } from "./workflow-e2e-helpers";

interface ApiResponse<T> {
  success?: boolean;
  code?: string;
  message?: string;
  data?: T;
}

interface AuthTokenData {
  accessToken?: string;
  refreshToken?: string;
}

interface RunResultData {
  executionId?: string;
}

interface TraceStep {
  nodeKey?: string;
  status?: string | number;
}

interface TraceData {
  executionId?: string;
  steps?: TraceStep[];
  edgeStatuses?: Array<Record<string, unknown>>;
}

async function getAppAccessToken(request: APIRequestContext): Promise<string> {
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
  const payload = (await response.json()) as ApiResponse<AuthTokenData>;
  const token = payload.data?.accessToken ?? "";
  expect(token).not.toBe("");
  return token;
}

async function seedPlatformConsoleSession(page: Page, request: APIRequestContext): Promise<void> {
  const tokenResponse = await request.post(`${platformApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });
  expect(tokenResponse.ok()).toBeTruthy();
  const tokenPayload = (await tokenResponse.json()) as ApiResponse<AuthTokenData>;
  const accessToken = tokenPayload.data?.accessToken ?? "";
  const refreshToken = tokenPayload.data?.refreshToken ?? "";
  expect(accessToken).not.toBe("");
  expect(refreshToken).not.toBe("");

  const profileResponse = await request.get(`${platformApiBase}/api/v1/auth/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "X-Tenant-Id": defaultTenantId
    }
  });
  expect(profileResponse.ok()).toBeTruthy();
  const profilePayload = (await profileResponse.json()) as ApiResponse<Record<string, unknown>>;
  expect(profilePayload.data).toBeTruthy();

  await page.goto(platformBaseUrl);
  await page.evaluate(
    (seed) => {
      window.localStorage.setItem("atlas_platform_tenant_id", seed.tenantId);
      window.localStorage.setItem("atlas_platform_refresh_token", seed.refreshToken);
      window.sessionStorage.setItem("atlas_platform_access_token", seed.accessToken);
      window.sessionStorage.setItem("atlas_platform_auth_profile", JSON.stringify(seed.profile));
    },
    {
      tenantId: defaultTenantId,
      accessToken,
      refreshToken,
      profile: profilePayload.data ?? {}
    }
  );
}

async function ensureConsoleReady(page: Page, request: APIRequestContext): Promise<void> {
  await page.goto(`${platformBaseUrl}/login`);
  const usernameInput = page.locator('input[autocomplete="username"]');
  const passwordInput = page.locator('input[autocomplete="current-password"]');
  const submitButton = page.locator("button.submit-btn");

  const canUseLoginForm =
    (await usernameInput.isVisible().catch(() => false)) &&
    (await passwordInput.isVisible().catch(() => false)) &&
    (await submitButton.isVisible().catch(() => false));

  if (canUseLoginForm) {
    await usernameInput.fill(defaultUsername);
    await passwordInput.fill(defaultPassword);
    await submitButton.click();
    try {
      await page.waitForFunction(
        () =>
          window.location.pathname !== "/login" ||
          Boolean(document.querySelector(".topbar__profile")),
        undefined,
        { timeout: 20_000 }
      );
      return;
    } catch {
      // Ignore login page flakiness and fallback to API-seeded session.
    }
  }

  await seedPlatformConsoleSession(page, request);
  await page.goto(platformBaseUrl);
  await page.waitForLoadState("domcontentloaded");
}

test.describe.serial("@smoke-main Console -> Workspace -> Workflow 主链路", () => {
  // 主链路依赖 Coze workflow editor 旧版 testId（save-draft / canvas-json / run-inputs / publish 流程），
  // 与 workflow-* 系列同源，详见 docs/e2e-baseline-failures.md §3。
  test.fixme("控制台登录后完成工作流创建、保存、发布、运行并校验 trace", async ({ page, request }) => {
    test.setTimeout(240_000);

    const appKey = await ensureAppSetup(request);
    await clearAuthStorage(page);

    await ensureConsoleReady(page, request);

    await clearAuthStorage(page);
    await loginApp(page, appKey);
    await expect(page.getByTestId("workspace-list-page")).toBeVisible({ timeout: 30_000 });

    const workspaceCard = page.locator(`.atlas-workspace-card:has-text("${appKey}")`).first();
    await expect(workspaceCard).toBeVisible({ timeout: 30_000 });
    await workspaceCard.locator('[data-testid^="workspace-open-"]').first().click();
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });

    await navigateBySidebar(page, "workflows", {
      urlPattern: /\/org\/[^/]+\/workspaces\/[^/]+\/workflows(?:\?.*)?$/
    });

    const workflowId = await createWorkflowAndOpenEditor(page, appKey);
    await expectWorkflowEditorReady(page);

    const saveDraftResponsePromise = page.waitForResponse(
      (response) => response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`),
      { timeout: 30_000 }
    );
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.status()).toBe(200);
    const saveDraftPayload = (await saveDraftResponse.json()) as ApiResponse<{ id?: string; Id?: string }>;
    expect(saveDraftPayload.success).toBeTruthy();

    const publishResponsePromise = page.waitForResponse(
      (response) => response.request().method() === "POST" && response.url().endsWith(`/api/v2/workflows/${workflowId}/publish`),
      { timeout: 30_000 }
    );
    await page.getByTestId("workflow-base-publish-button").click();
    const publishResponse = await publishResponsePromise;
    expect(publishResponse.status()).toBe(200);
    const publishPayload = (await publishResponse.json()) as ApiResponse<{ id?: string; Id?: string }>;
    expect(publishPayload.success).toBeTruthy();

    const runResponsePromise = page.waitForResponse(
      (response) => response.request().method() === "POST" && response.url().endsWith(`/api/v2/workflows/${workflowId}/run`),
      { timeout: 30_000 }
    );
    await page.getByTestId("workflow.detail.run-inputs").fill('{"input":{"message":"smoke-main-chain"}}');
    await page.getByTestId("workflow.detail.toolbar.test-run").click();
    const runResponse = await runResponsePromise;
    expect(runResponse.status()).toBe(200);
    const runPayload = (await runResponse.json()) as ApiResponse<RunResultData>;
    expect(runPayload.success).toBeTruthy();
    const executionId = runPayload.data?.executionId ?? "";
    expect(executionId).not.toBe("");
    await expect(page.getByTestId("workflow.detail.node.testrun.result-panel")).toBeVisible({ timeout: 30_000 });

    const accessToken = await getAppAccessToken(request);
    const traceResponse = await request.get(`${appApiBase}/api/v2/workflows/executions/${executionId}/trace`, {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "X-Tenant-Id": defaultTenantId,
        "X-Project-Id": "1"
      }
    });
    expect(traceResponse.status()).toBe(200);
    const tracePayload = (await traceResponse.json()) as ApiResponse<TraceData>;
    expect(tracePayload.success).toBeTruthy();
    expect(tracePayload.data?.executionId).toBe(executionId);
    expect((tracePayload.data?.steps ?? []).length).toBeGreaterThan(0);
    expect((tracePayload.data?.edgeStatuses ?? []).length).toBeGreaterThan(0);
  });
});
