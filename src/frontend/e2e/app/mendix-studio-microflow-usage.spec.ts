import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultPassword, defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function expression(raw: string) {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
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
  const name = uniqueName("E2E_MF_USAGE").replace(/-/g, "_");
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
        tags: ["e2e", "usage"],
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

async function saveUsageSchema(page: import("@playwright/test").Page, microflowId: string) {
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
      id: "create-level",
      type: "actionActivity",
      data: {
        objectId: "create-level",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Create Variable",
        subtitle: "createVariable",
        documentation: "",
        collectionId: "root-collection",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "createVariable",
        action: {
          id: "action-create-level",
          kind: "createVariable",
          officialType: "Microflows$CreateVariableAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "variable", iconKey: "variable", availability: "supported" },
          variableName: "approvalLevel",
          dataType: { kind: "string" },
          initialValue: expression("'L1'"),
          readonly: false,
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 320, y: 180 }, size: { width: 110, height: 36 } },
    },
    {
      id: "change-level",
      type: "actionActivity",
      data: {
        objectId: "change-level",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Change Variable",
        subtitle: "changeVariable",
        documentation: "",
        collectionId: "root-collection",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "changeVariable",
        action: {
          id: "action-change-level",
          kind: "changeVariable",
          officialType: "Microflows$ChangeVariableAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "variable", iconKey: "variable", availability: "supported" },
          targetVariableName: "approvalLevel",
          newValueExpression: expression("if true then 'L2' else $approvalLevel"),
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 520, y: 180 }, size: { width: 110, height: 36 } },
    },
  ];

  baseSchema.workflow.edges = [
    edge("flow-start-create", "start", "create-level"),
    edge("flow-create-change", "create-level", "change-level"),
    edge("flow-change-end", "change-level", "end"),
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
      saveReason: "e2e-usage-highlight",
      clientRequestId: `e2e-usage-highlight-${Date.now()}`,
      force: true,
    },
  });
  expect(saveResp.ok()).toBeTruthy();
}

async function openUsagePage(page: import("@playwright/test").Page, appKey: string) {
  void appKey;
  if (!/\/space\/[^/]+\//.test(page.url()) && !/\/workspace\/[^/]+\//.test(page.url())) {
    await page.goto(`${appBaseUrl}${selectWorkspacePath()}`, { waitUntil: "domcontentloaded" });
    const onSelectPage = await page.getByTestId("coze-select-workspace-page").isVisible({ timeout: 5_000 }).catch(() => false);
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
  const workspaceId = readWorkspaceIdFromUrl(page.url());
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  const microflow = await createMicroflow(page, workspaceId);
  await saveUsageSchema(page, microflow.id);
  await page.goto(`${appBaseUrl}/microflow/${encodeURIComponent(microflow.id)}/editor`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("microflow-node-create-level")).toBeVisible();
  await expect(page.getByTestId("microflow-node-change-level")).toBeVisible();
}

async function ensurePropertiesPanelVisible(page: import("@playwright/test").Page) {
  const propertyPanel = page.getByTestId("microflow-property-panel");
  if (await propertyPanel.isVisible().catch(() => false)) {
    return propertyPanel;
  }
  const expandButton = page.getByTestId("microflow-editor-right-shell").locator('button[title="Properties"]').first();
  await expandButton.evaluate(node => (node as HTMLButtonElement).click());
  await expect(propertyPanel).toBeVisible({ timeout: 15_000 });
  return propertyPanel;
}

async function loginForUsage(page: import("@playwright/test").Page, appKey: string) {
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
}

test.describe.serial("@microflow Mendix Studio Usage Highlight", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("选中节点时高亮来源节点并标记消费者 Usage", async ({ page }) => {
    await loginForUsage(page, appKey);
    await openUsagePage(page, appKey);

    const createNode = page.getByTestId("microflow-node-create-level");
    const changeNode = page.getByTestId("microflow-node-change-level");

    await changeNode.click({ position: { x: 18, y: 18 } });
    await expect(createNode).toHaveClass(/is-usage-source/);

    await createNode.click({ position: { x: 18, y: 18 } });
    await expect(changeNode).toHaveClass(/is-usage-consumer/);
    await expect(changeNode.getByText("Usage")).toBeVisible();
  });

  test("点击属性面板变量名时高亮所有使用者", async ({ page }) => {
    await loginForUsage(page, appKey);
    await openUsagePage(page, appKey);

    const shell = page.getByTestId("microflow-editor-shell");
    const createNode = page.getByTestId("microflow-node-create-level");
    const changeNode = page.getByTestId("microflow-node-change-level");

    await createNode.click({ position: { x: 18, y: 18 } });
    const propertyPanel = await ensurePropertiesPanelVisible(page);
    await createNode.click({ position: { x: 18, y: 18 } });
    await expect(page.getByTestId("microflow-output-variable--approvalLevel")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("microflow-output-variable--approvalLevel").click();

    await expect(shell).toHaveAttribute("data-usage-selected-variable", "$approvalLevel");
    await expect(changeNode).toHaveClass(/is-usage-consumer/);
    await expect(changeNode.getByText("Usage")).toBeVisible();
  });
});
