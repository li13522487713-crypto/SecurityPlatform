import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultPassword, defaultTenantId, ensureAppSetup, uniqueName } from "./helpers";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function edge(id: string, sourceNodeID: string, targetNodeID: string, data: Record<string, unknown> = {}) {
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
      ...data,
    },
  };
}

function readWorkspaceIdFromUrl(url: string): string | undefined {
  return url.match(/\/space\/([^/]+)/)?.[1]
    ?? url.match(/\/workspace\/([^/]+)/)?.[1];
}

let authenticatedAccessToken = "";

async function createMicroflow(page: import("@playwright/test").Page, workspaceId: string) {
  const name = uniqueName("E2E_MF_LOOP_COMMIT").replace(/-/g, "_");
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
        tags: ["e2e", "loop-commit"],
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

async function saveLoopCommitSchema(page: import("@playwright/test").Page, microflowId: string) {
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
  const loopBodyCollectionId = "loop-body";

  baseSchema.workflow.nodes = [
    ...baseSchema.workflow.nodes.filter((node: { id?: string }) => node.id === "start" || node.id === "end"),
    {
      id: "loop-node",
      type: "loopedActivity",
      data: {
        objectId: "loop-node",
        objectKind: "loopedActivity",
        officialType: "Microflows$LoopedActivity",
        title: "Loop",
        documentation: "",
        collectionId: "root-collection",
        bodyCollectionId: loopBodyCollectionId,
        loopSource: {
          kind: "iterableList",
          listVariableName: "orders",
          iteratorVariableName: "orderItem",
          iteratorVariableDataType: { kind: "object", entityQualifiedName: "Sales.Order" },
        },
        errorHandlingType: "rollback",
        disabled: false,
      },
      meta: { nodeDTOType: "loopedActivity", collectionId: "root-collection", position: { x: 360, y: 180 }, size: { width: 320, height: 190 } },
    },
    {
      id: "commit-in-loop",
      type: "actionActivity",
      data: {
        objectId: "commit-in-loop",
        objectKind: "actionActivity",
        officialType: "Microflows$ActionActivity",
        title: "Commit Object(s)",
        subtitle: "commit",
        documentation: "",
        collectionId: loopBodyCollectionId,
        parentObjectId: "loop-node",
        autoGenerateCaption: false,
        backgroundColor: "default",
        disabled: false,
        actionKind: "commit",
        action: {
          id: "action-commit-in-loop",
          kind: "commit",
          officialType: "Microflows$CommitAction",
          errorHandlingType: "rollback",
          documentation: "",
          editor: { category: "object", iconKey: "commit", availability: "supported" },
          objectOrListVariableName: "orderItem",
          entityQualifiedName: "Sales.Order",
          entityType: "Sales.Order",
        },
      },
      meta: { nodeDTOType: "actionActivity", collectionId: loopBodyCollectionId, parentObjectId: "loop-node", position: { x: 360, y: 320 }, size: { width: 110, height: 36 } },
    },
  ];

  baseSchema.workflow.edges = [
    edge("flow-start-loop", "start", "loop-node"),
    edge("flow-loop-end", "loop-node", "end"),
    edge("flow-loop-body-commit", "loop-node", "commit-in-loop", {
      edgeKind: "loopBody",
      flowKind: "sequence",
      collectionId: loopBodyCollectionId,
    }),
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
      saveReason: "e2e-loop-commit",
      clientRequestId: `e2e-loop-commit-${Date.now()}`,
      force: true,
    },
  });
  expect(saveResp.ok()).toBeTruthy();
}

async function loginForLoopCommit(page: import("@playwright/test").Page, appKey: string) {
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

async function openLoopCommitPage(page: import("@playwright/test").Page, appKey: string) {
  const workspaceId = readWorkspaceIdFromUrl(page.url());
  if (!workspaceId) {
    throw new Error(`Workspace id not found in ${page.url()}`);
  }
  const microflow = await createMicroflow(page, workspaceId);
  await saveLoopCommitSchema(page, microflow.id);
  await page.goto(`${appBaseUrl}/microflow/${encodeURIComponent(microflow.id)}/editor`, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("microflow-editor-shell")).toBeVisible({ timeout: 30_000 });
}

test.describe.serial("@microflow Mendix Studio Loop Commit", () => {
  test.describe.configure({ timeout: 300_000 });
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    test.setTimeout(300_000);
    appKey = await ensureAppSetup(request);
  });

  test("Loop 内 Commit 会在 Problems 面板显示 LOOP_COMMIT", async ({ page }) => {
    await loginForLoopCommit(page, appKey);
    await openLoopCommitPage(page, appKey);

    await page.getByTestId("microflow-editor-validate").click();
    await page.getByTestId("microflow-bottom-status-strip").getByRole("button", { name: /Problems|问题/ }).click();
    await expect(page.getByTestId("microflow-bottom-panel")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("button", { name: /LOOP_COMMIT/ }).first()).toBeVisible({ timeout: 20_000 });
  });
});
