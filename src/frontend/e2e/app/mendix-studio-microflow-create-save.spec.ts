import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultTenantId, ensureAppSetup, navigateBySidebar, uniqueName } from "./helpers";

function withLogNode(schema: any, message: string) {
  const next = JSON.parse(JSON.stringify(schema));
  const logNode = {
    id: "e2e-log-node",
    type: "actionActivity",
    data: {
      objectId: "e2e-log-node",
      objectKind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      title: "E2E Log",
      documentation: "",
      collectionId: "root-collection",
      autoGenerateCaption: false,
      backgroundColor: "default",
      disabled: false,
      actionKind: "logMessage",
      action: {
        id: "action-e2e-log-node",
        kind: "logMessage",
        officialType: "Microflows$LogMessageAction",
        errorHandlingType: "rollback",
        documentation: "E2E save verification",
        editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
        level: "info",
        logNodeName: "MicroflowE2E",
        template: { text: message, arguments: [] },
        includeContextVariables: false,
        includeTraceId: true
      }
    },
    meta: { nodeDTOType: "microflow", collectionId: "root-collection", position: { x: 440, y: 200 }, size: { width: 152, height: 72 } }
  };
  next.workflow.nodes = next.workflow.nodes.filter((item: { id?: string }) => item.id !== logNode.id);
  next.workflow.nodes.push(logNode);
  next.workflow.edges = [
    {
      id: "flow-start-e2e-log",
      sourceNodeID: "start",
      targetNodeID: logNode.id,
      data: { flowId: "flow-start-e2e-log", flowKind: "sequence", edgeKind: "sequence", caseValues: [], isErrorHandler: false, line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } } }
    },
    {
      id: "flow-e2e-log-end",
      sourceNodeID: logNode.id,
      targetNodeID: "end",
      data: { flowId: "flow-e2e-log-end", flowKind: "sequence", edgeKind: "sequence", caseValues: [], isErrorHandler: false, line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } } }
    }
  ];
  next.audit = { ...(next.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };
  return next;
}

test.describe.serial("@microflow Mendix studio create/save", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("create microflow from studio and deep-link open", async ({ page }) => {
    await navigateBySidebar(page, "projects", { urlPattern: /\/workspace\/[^/]+\/projects(?:\?.*)?$/ });
    const workspaceId = page.url().match(/\/workspace\/([^/]+)/)?.[1];
    expect(workspaceId).toBeTruthy();

    const appCard = page.locator('[data-testid="workspace-project-card"]').first();
    if (await appCard.count()) {
      await appCard.click();
    } else {
      await page.goto(`${appBaseUrl}/space/${encodeURIComponent(String(workspaceId))}/mendix-studio/${encodeURIComponent(appKey)}`);
    }

    await page.waitForURL(/\/space\/[^/]+\/mendix-studio\/[^/?]+(?:\?.*)?$/);
    await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });

    const name = uniqueName("E2E_MF_CREATE").replace(/-/g, "_");
    await page.request.post(`${appApiBase}/api/v1/microflows`, {
      headers: { "Content-Type": "application/json", "X-Tenant-Id": defaultTenantId },
      data: {
        workspaceId,
        input: {
          name,
          displayName: name,
          moduleId: "Sales",
          moduleName: "Sales",
          tags: ["e2e"],
          parameters: [],
          returnType: { kind: "void" },
          template: "blank"
        }
      }
    });

    const listResp = await page.request.get(`${appApiBase}/api/v1/microflows?workspaceId=${encodeURIComponent(String(workspaceId))}`, {
      headers: { "X-Tenant-Id": defaultTenantId }
    });
    expect(listResp.ok()).toBeTruthy();
    const listJson = await listResp.json();
    const created = (listJson?.data?.items ?? []).find((item: { name?: string; id?: string }) => item.name === name);
    expect(created?.id).toBeTruthy();

    await page.goto(`${appBaseUrl}/space/${encodeURIComponent(String(workspaceId))}/mendix-studio/${encodeURIComponent(appKey)}?microflowId=${encodeURIComponent(String(created.id))}`);
    await page.waitForURL(/microflowId=/);
    await expect(page.locator(".mendix-studio-root")).toBeVisible();

    const schemaResp = await page.request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(String(created.id))}/schema`, {
      headers: { "X-Tenant-Id": defaultTenantId, "X-Workspace-Id": String(workspaceId) }
    });
    expect(schemaResp.ok()).toBeTruthy();
    const schemaPayload = await schemaResp.json();
    const editedSchema = withLogNode(schemaPayload?.data?.schema, `Saved from E2E ${name}`);

    const saveResp = await page.request.put(`${appApiBase}/api/v1/microflows/${encodeURIComponent(String(created.id))}/schema`, {
      headers: { "Content-Type": "application/json", "X-Tenant-Id": defaultTenantId, "X-Workspace-Id": String(workspaceId) },
      data: {
        schema: editedSchema,
        baseVersion: schemaPayload?.data?.schemaVersion,
        saveReason: "e2e-create-edit-save",
        clientRequestId: `e2e-save-${name}`
      }
    });
    expect(saveResp.ok()).toBeTruthy();

    const reloadResp = await page.request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(String(created.id))}/schema`, {
      headers: { "X-Tenant-Id": defaultTenantId, "X-Workspace-Id": String(workspaceId) }
    });
    expect(reloadResp.ok()).toBeTruthy();
    const reloadedSchema = (await reloadResp.json())?.data?.schema;
    const savedLogNode = reloadedSchema?.workflow?.nodes?.find((item: { id?: string }) => item.id === "e2e-log-node");
    expect(savedLogNode?.data?.action?.template?.text).toBe(`Saved from E2E ${name}`);
    expect(reloadedSchema?.workflow?.edges?.some((flow: { id?: string }) => flow.id === "flow-start-e2e-log")).toBeTruthy();
  });
});
