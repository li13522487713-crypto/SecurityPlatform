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

let authenticatedAccessToken = "";

function authHeaders(accessToken: string): Record<string, string> {
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${accessToken}`,
    "X-Tenant-Id": defaultTenantId,
  };
}

async function ensureWorkspaceId(page: import("@playwright/test").Page, accessToken: string): Promise<string> {
  const listResponse = await page.request.get(`${appApiBase}/v1/workspaces?page_num=1&page_size=20`, {
    headers: authHeaders(accessToken),
  });
  expect(listResponse.ok()).toBeTruthy();
  const listPayload = await listResponse.json() as {
    data?: { workspaces?: Array<{ id?: string | number }> };
  };
  const existingWorkspaceId = String(listPayload?.data?.workspaces?.[0]?.id ?? "").trim();
  if (existingWorkspaceId) {
    return existingWorkspaceId;
  }

  const createResponse = await page.request.post(`${appApiBase}/v1/workspaces`, {
    headers: authHeaders(accessToken),
    data: {
      name: uniqueName("e2e-step-into-workspace"),
      description: "created by playwright step-into e2e",
    },
  });
  expect(createResponse.ok()).toBeTruthy();
  const createPayload = await createResponse.json() as {
    success?: boolean;
    code?: number | string;
    data?: { id?: string | number; workspace_id?: string | number };
  };
  const success = createPayload.success === true || Number(createPayload.code) === 0;
  expect(success).toBeTruthy();
  const workspaceId = String(createPayload.data?.id ?? createPayload.data?.workspace_id ?? "").trim();
  expect(workspaceId).not.toBe("");
  return workspaceId;
}

async function loginForWorkbench(page: import("@playwright/test").Page): Promise<string> {
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

  const workspaceId = await ensureWorkspaceId(page, accessToken);
  await page.goto(`${appBaseUrl}${signPath()}`, { waitUntil: "domcontentloaded" });
  await page.evaluate(({ token, tenantId, profile, nextWorkspaceId }) => {
    window.localStorage.setItem("atlas_app_access_token", token);
    window.localStorage.setItem("atlas_app_tenant_id", tenantId);
    window.localStorage.setItem("atlas_last_workspace_id", nextWorkspaceId);
    window.localStorage.removeItem("atlas_app_auth_profile");
    window.sessionStorage.setItem("atlas_app_auth_profile", JSON.stringify(profile));
  }, {
    token: accessToken,
    tenantId: defaultTenantId,
    profile: mePayload?.data ?? {},
    nextWorkspaceId: workspaceId,
  });
  return workspaceId;
}

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string, prefix: string) {
  const name = uniqueName(prefix).replace(/-/g, "_");
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
        tags: ["e2e", "step-into"],
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

async function createWorkspaceApp(page: import("@playwright/test").Page, workspaceId: string) {
  const response = await page.request.post(`${appApiBase}/api/v1/workspace-ide/apps`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
    data: {
      name: uniqueName("E2E_MENDIX_APP"),
      description: "created by playwright step-into e2e",
      workspaceId,
    },
  });
  expect(response.ok()).toBeTruthy();
  const payload = await response.json() as {
    success?: boolean;
    data?: { appId?: string; entryRoute?: string };
  };
  expect(payload.success).toBeTruthy();
  expect(payload.data?.appId).toBeTruthy();
  return {
    appId: String(payload.data?.appId),
    entryRoute: String(payload.data?.entryRoute ?? ""),
  };
}

async function readSchema(page: import("@playwright/test").Page, microflowId: string) {
  const schemaResp = await page.request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(microflowId)}/schema`, {
    headers: {
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
  });
  expect(schemaResp.ok()).toBeTruthy();
  return schemaResp.json();
}

async function writeSchema(page: import("@playwright/test").Page, microflowId: string, schema: unknown, baseVersion: unknown, reason: string) {
  const saveResp = await page.request.put(`${appApiBase}/api/v1/microflows/${encodeURIComponent(microflowId)}/schema`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId,
      Authorization: `Bearer ${authenticatedAccessToken}`,
    },
    data: {
      schema,
      baseVersion,
      saveReason: reason,
      clientRequestId: `${reason}-${Date.now()}`,
      force: true,
    },
  });
  expect(saveResp.ok()).toBeTruthy();
}

async function saveChildSchema(page: import("@playwright/test").Page, microflowId: string) {
  const schemaPayload = await readSchema(page, microflowId);
  const baseSchema = clone(schemaPayload?.data?.schema);
  const baseVersion = schemaPayload?.data?.schemaVersion;

  baseSchema.workflow.nodes = [
    ...baseSchema.workflow.nodes.filter((node: { id?: string }) => node.id === "start" || node.id === "end"),
    {
      id: "child-log",
      type: "actionActivity",
      data: {
        objectId: "child-log",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Child Log",
        subtitle: "logMessage",
        documentation: "",
        collectionId: "root-collection",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "logMessage",
        action: {
          id: "action-child-log",
          kind: "logMessage",
          officialType: "Microflows$LogMessageAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
          level: "info",
          logNodeName: "ChildFlow",
          template: { text: "child-called", arguments: [] },
          includeContextVariables: true,
          includeTraceId: true,
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 320, y: 200 }, size: { width: 110, height: 36 } },
    },
  ];
  baseSchema.workflow.edges = [
    edge("flow-start-child-log", "start", "child-log"),
    edge("flow-child-log-end", "child-log", "end"),
  ];
  baseSchema.audit = { ...(baseSchema.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };

  await writeSchema(page, microflowId, baseSchema, baseVersion, "e2e-child-step-into-schema");
}

async function saveParentSchema(page: import("@playwright/test").Page, microflowId: string, child: { id: string; name: string }) {
  const schemaPayload = await readSchema(page, microflowId);
  const baseSchema = clone(schemaPayload?.data?.schema);
  const baseVersion = schemaPayload?.data?.schemaVersion;

  baseSchema.workflow.nodes = [
    ...baseSchema.workflow.nodes.filter((node: { id?: string }) => node.id === "start" || node.id === "end"),
    {
      id: "call-child",
      type: "actionActivity",
      data: {
        objectId: "call-child",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Call Microflow",
        subtitle: "callMicroflow",
        documentation: "",
        collectionId: "root-collection",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "callMicroflow",
        action: {
          id: "action-call-child",
          kind: "callMicroflow",
          officialType: "Microflows$MicroflowCallAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "call", iconKey: "callMicroflow", availability: "supported" },
          targetMicroflowId: child.id,
          targetMicroflowQualifiedName: `Sales.${child.name}`,
          targetMicroflowDisplayName: child.name,
          targetMicroflowName: child.name,
          parameterMappings: [],
          returnValue: { storeResult: false, outputVariableName: "", dataType: { kind: "void" } },
          callMode: "sync",
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 320, y: 200 }, size: { width: 110, height: 36 } },
    },
  ];
  baseSchema.workflow.edges = [
    edge("flow-start-call-child", "start", "call-child"),
    edge("flow-call-child-end", "call-child", "end"),
  ];
  baseSchema.audit = { ...(baseSchema.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };

  await writeSchema(page, microflowId, baseSchema, baseVersion, "e2e-parent-step-into-schema");
}

async function openWorkbenchParentPage(page: import("@playwright/test").Page, appKey: string, workspaceId: string) {
  void appKey;
  const workspaceApp = await createWorkspaceApp(page, workspaceId);
  const child = await createMicroflow(page, workspaceId, "E2E_MF_STEP_CHILD");
  const parent = await createMicroflow(page, workspaceId, "E2E_MF_STEP_PARENT");
  await saveChildSchema(page, child.id);
  await saveParentSchema(page, parent.id, child);
  await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/${encodeURIComponent(workspaceApp.appId)}?microflowId=${encodeURIComponent(parent.id)}`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-resource-editor-host")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
  return { parent, child };
}

async function stepIntoUntilChildTab(page: import("@playwright/test").Page, childMicroflowId: string, childName: string) {
  const childHost = page.locator(`[data-testid="microflow-resource-editor-host"][data-microflow-id="${childMicroflowId}"]`);
  for (let attempt = 0; attempt < 5; attempt += 1) {
    if (await childHost.count() > 0 && await childHost.isVisible()) {
      return;
    }
    const button = page.getByRole("button", { name: "Step Into" });
    await expect(button).toBeEnabled({ timeout: 20_000 });
    await button.click();
    const activeTab = page.locator(".studio-workbench-tab--active");
    await expect(activeTab).toContainText(new RegExp(`${childName}|E2E_MF_STEP_PARENT`, "i"), { timeout: 20_000 });
    await page.waitForTimeout(600);
  }
  await expect(childHost).toBeVisible({ timeout: 30_000 });
}

test.describe.serial("@microflow Mendix Studio Step Into", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("调试 Step Into 调用子微流后，会自动打开并切到子微流画布", async ({ page }) => {
    await loginForWorkbench(page);
    await page.goto(`${appBaseUrl}${selectWorkspacePath()}`, { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/space\/[^/]+\/(?:home|projects)(?:\?.*)?$/, { timeout: 30_000 });
    const workspaceId = decodeURIComponent(new URL(page.url()).pathname.match(/^\/space\/([^/]+)\//)?.[1] ?? "");
    expect(workspaceId).not.toBe("");
    authenticatedAccessToken = await page.evaluate(() =>
      window.localStorage.getItem("atlas_app_access_token")
      ?? window.sessionStorage.getItem("atlas_app_access_token")
      ?? ""
    );
    expect(authenticatedAccessToken).not.toBe("");
    const { child } = await openWorkbenchParentPage(page, appKey, workspaceId);

    await page.getByTestId("microflow-workbench-debug-run").click();
    await expect(page.getByTestId("microflow-test-run-modal-content")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("microflow-test-run-submit").click();
    await expect(page.getByTestId("microflow-test-run-modal")).toHaveCount(0, { timeout: 30_000 });
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible({ timeout: 30_000 });

    await stepIntoUntilChildTab(page, child.id, child.name);

    await expect(page.locator(".studio-workbench-tab--active")).toContainText(child.name);
    await expect(page.locator(`[data-testid="microflow-resource-editor-host"][data-microflow-id="${child.id}"]`)).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible();
    await expect(page.getByRole("button", { name: "Step Into" })).toBeVisible();
  });
});
