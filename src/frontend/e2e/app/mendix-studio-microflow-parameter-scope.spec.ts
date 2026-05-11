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
  const name = uniqueName("E2E_MF_PARAM_SCOPE").replace(/-/g, "_");
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
        tags: ["e2e", "parameter-scope"],
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

async function saveParameterSchema(page: import("@playwright/test").Page, microflowId: string) {
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

  baseSchema.parameters = [
    {
      id: "param-amount",
      stableId: "param-amount",
      name: "amount",
      dataType: { kind: "decimal" },
      type: { kind: "primitive", name: "Decimal" },
      required: true,
      description: "审批金额",
      documentation: "审批金额",
    },
  ];

  baseSchema.workflow.nodes = [
    ...baseSchema.workflow.nodes.filter((node: { id?: string }) => node.id === "start" || node.id === "end"),
    {
      id: "parameter-amount",
      type: "parameterObject",
      data: {
        objectId: "parameter-amount",
        objectKind: "parameterObject",
        officialType: "Microflows$MicroflowParameterObject",
        title: "amount",
        documentation: "",
        collectionId: "root-collection",
        parameterId: "param-amount",
        parameterName: "amount",
      },
      meta: { nodeDTOType: "parameterObject", collectionId: "root-collection", position: { x: 120, y: 80 }, size: { width: 158, height: 70 } },
    },
    {
      id: "create-variable",
      type: "actionActivity",
      data: {
        objectId: "create-variable",
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
          id: "action-create-variable",
          kind: "createVariable",
          officialType: "Microflows$CreateVariableAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "variable", iconKey: "variable", availability: "supported" },
          variableName: "threshold",
          dataType: { kind: "decimal" },
          initialValue: expression("$amount"),
          readonly: false,
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 320, y: 220 }, size: { width: 110, height: 36 } },
    },
  ];

  baseSchema.workflow.edges = [
    edge("flow-start-create", "start", "create-variable"),
    edge("flow-create-end", "create-variable", "end"),
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
      saveReason: "e2e-parameter-scope",
      clientRequestId: `e2e-parameter-scope-${Date.now()}`,
      force: true,
    },
  });
  expect(saveResp.ok()).toBeTruthy();
}

async function loginForParameterScope(page: import("@playwright/test").Page, appKey: string) {
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

async function openParameterScopePage(page: import("@playwright/test").Page, appKey: string) {
  const workspaceId = readWorkspaceIdFromUrl(page.url());
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  const microflow = await createMicroflow(page, workspaceId);
  await saveParameterSchema(page, microflow.id);
  await page.goto(`${appBaseUrl}/microflow/${encodeURIComponent(microflow.id)}/editor`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
}

test.describe.serial("@microflow Mendix Studio Parameter Scope", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("参数节点显示在 Start 上方，且后续节点引用参数不会报变量缺失", async ({ page }) => {
    await loginForParameterScope(page, appKey);
    await openParameterScopePage(page, appKey);

    const parameterNode = page.getByTestId("microflow-node-parameter-amount");
    const startNode = page.getByTestId("microflow-node-start");
    await expect(parameterNode).toBeVisible({ timeout: 20_000 });
    await expect(startNode).toBeVisible({ timeout: 20_000 });

    const parameterBox = await parameterNode.boundingBox();
    const startBox = await startNode.boundingBox();
    expect(parameterBox).toBeTruthy();
    expect(startBox).toBeTruthy();
    expect(parameterBox!.y).toBeLessThan(startBox!.y);

    await page.getByTestId("microflow-editor-validate").click();
    await page.getByTestId("microflow-bottom-status-strip").getByRole("button", { name: /Problems|问题/ }).click();
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("MF_VARIABLE_REFERENCE_UNKNOWN")).toHaveCount(0);
    await expect(page.getByText("MF_VARIABLE_NOT_FOUND")).toHaveCount(0);
  });
});
