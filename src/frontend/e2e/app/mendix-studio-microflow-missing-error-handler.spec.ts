import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultPassword, defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function edge(id: string, sourceNodeID: string, targetNodeID: string) {
  return {
    id,
    sourceNodeID,
    targetNodeID,
    data: {
      flowId: id,
      flowKind: "sequence",
      edgeKind: "sequence",
      caseValues: [],
      isErrorHandler: false,
      line: {
        kind: "orthogonal",
        points: [],
        routing: { mode: "auto", bendPoints: [] },
        style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
      },
    },
  };
}

function readWorkspaceIdFromUrl(url: string): string | undefined {
  return url.match(/\/space\/([^/]+)/)?.[1]
    ?? url.match(/\/workspace\/([^/]+)/)?.[1];
}

let authenticatedAccessToken = "";

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string) {
  const name = uniqueName("E2E_MF_ERROR_WARN").replace(/-/g, "_");
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
        tags: ["e2e", "missing-error-handler"],
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

async function saveWarningSchema(page: import("@playwright/test").Page, microflowId: string) {
  const schemaResp = await page.request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(microflowId)}/schema`, {
    headers: {
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
  });
  expect(schemaResp.ok()).toBeTruthy();
  const schemaPayload = await schemaResp.json();
  const baseSchema = clone(schemaPayload?.data?.schema);
  const baseVersion = schemaPayload?.data?.schemaVersion;

  baseSchema.workflow.nodes = [
    ...baseSchema.workflow.nodes.filter((node: { id?: string }) => node.id === "start" || node.id === "end"),
    {
      id: "call-java",
      type: "actionActivity",
      data: {
        objectId: "call-java",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Call Java Action",
        subtitle: "callJavaAction",
        documentation: "",
        collectionId: "root-collection",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "callJavaAction",
        action: {
          id: "action-call-java",
          kind: "callJavaAction",
          officialType: "Microflows$JavaActionCallAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "integration", iconKey: "callJavaAction", availability: "supported" },
          javaActionQualifiedName: "Sales.DoWork",
          outputVariableName: "",
          returnType: { kind: "void" },
          parameterMappings: [],
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 360, y: 180 }, size: { width: 110, height: 36 } },
    },
  ];

  baseSchema.workflow.edges = [
    edge("flow-start-call-java", "start", "call-java"),
    edge("flow-call-java-end", "call-java", "end"),
  ];
  baseSchema.audit = { ...(baseSchema.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };

  const saveResp = await page.request.put(`${appApiBase}/api/v1/microflows/${encodeURIComponent(microflowId)}/schema`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
    data: {
      schema: baseSchema,
      baseVersion,
      saveReason: "e2e-missing-error-handler",
      clientRequestId: `e2e-missing-error-handler-${Date.now()}`,
      force: true,
    },
  });
  expect(saveResp.ok()).toBeTruthy();
}

async function loginForWarning(page: import("@playwright/test").Page, appKey: string) {
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

async function openWarningPage(page: import("@playwright/test").Page, appKey: string) {
  const workspaceId = readWorkspaceIdFromUrl(page.url());
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  const microflow = await createMicroflow(page, workspaceId);
  await saveWarningSchema(page, microflow.id);
  await page.goto(`${appBaseUrl}/microflow/${encodeURIComponent(microflow.id)}/editor`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
}

test.describe.serial("@microflow Mendix Studio Missing Error Handler", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("集成调用缺少错误处理时在 Problems 面板显示 MISSING_ERROR_HANDLER", async ({ page }) => {
    await loginForWarning(page, appKey);
    await openWarningPage(page, appKey);

    await page.getByTestId("microflow-editor-validate").click();
    await page.getByTestId("microflow-bottom-status-strip").getByRole("button", { name: /Problems|问题/ }).click();
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("MISSING_ERROR_HANDLER")).toHaveCount(1, { timeout: 20_000 });
  });
});
