import { expect, test } from "../fixtures/single-session";
import { appApiBase, appBaseUrl, defaultTenantId, ensureAppSetup } from "./helpers";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function createBrokenPublishSchema(schema: any) {
  const next = clone(schema);
  next.flows = [];
  next.audit = { ...(next.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };
  return next;
}

function createRunnableLogSchema(schema: any, message: string) {
  const next = clone(schema);
  const logNode = {
    id: "e2e-runtime-log",
    stableId: "e2e-runtime-log",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "E2E Runtime Log",
    documentation: "",
    relativeMiddlePoint: { x: 440, y: 200 },
    size: { width: 168, height: 72 },
    editor: { iconKey: "logMessage" },
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: false,
    action: {
      id: "action-e2e-runtime-log",
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      errorHandlingType: "rollback",
      documentation: "E2E publish and runtime verification",
      editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
      level: "info",
      logNodeName: "MicroflowE2E",
      template: { text: message, arguments: [] },
      includeContextVariables: true,
      includeTraceId: true
    }
  };
  next.objectCollection.objects = next.objectCollection.objects.filter((item: { id?: string }) => item.id !== logNode.id);
  next.objectCollection.objects.push(logNode);
  next.flows = [
    {
      id: "flow-start-runtime-log",
      stableId: "flow-start-runtime-log",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "start",
      destinationObjectId: logNode.id,
      originConnectionIndex: 0,
      destinationConnectionIndex: 0,
      caseValues: [],
      isErrorHandler: false,
      line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
      editor: { edgeKind: "sequence" }
    },
    {
      id: "flow-runtime-log-end",
      stableId: "flow-runtime-log-end",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: logNode.id,
      destinationObjectId: "end",
      originConnectionIndex: 0,
      destinationConnectionIndex: 0,
      caseValues: [],
      isErrorHandler: false,
      line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
      editor: { edgeKind: "sequence" }
    }
  ];
  next.audit = { ...(next.audit ?? {}), updatedAt: new Date().toISOString(), status: "draft" };
  return next;
}

async function openMendixStudio(page: import("@playwright/test").Page) {
  const workspaceMatch = new URL(page.url()).pathname.match(/^\/workspace\/([^/]+)\//);
  if (!workspaceMatch) {
    throw new Error(`当前页面不在 workspace 上下文: ${page.url()}`);
  }
  const workspaceId = decodeURIComponent(workspaceMatch[1]);
  await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/app_procurement`, {
    waitUntil: "domcontentloaded",
  });
}

test.describe.serial("Mendix Studio Microflow Runtime", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("通过 API 组合 Validate/Submit 并验证 test-run trace", async ({ page, request }) => {
    await openMendixStudio(page);

    const authToken = await page.evaluate(() => window.localStorage.getItem("atlas_app_access_token") ?? "");
    expect(authToken).not.toBe("");

    const workspaceId = decodeURIComponent(new URL(page.url()).pathname.match(/^\/space\/([^/]+)\//)?.[1] ?? "");
    expect(workspaceId).not.toBe("");

    const headers = {
      "Content-Type": "application/json",
      Authorization: `Bearer ${authToken}`,
      "X-Tenant-Id": defaultTenantId,
      "X-Workspace-Id": workspaceId,
      "X-User-Id": "admin"
    };

    const now = Date.now();
    const validateName = `E2E_MF_VALIDATE_${now}`;
    const submitName = `E2E_MF_SUBMIT_${now}`;
    const moduleId = "sales";

    const createValidate = await request.post(`${appApiBase}/api/v1/microflows`, {
      headers,
      data: {
        workspaceId,
        input: {
          name: validateName,
          displayName: validateName,
          description: "e2e validate flow",
          moduleId,
          moduleName: moduleId,
          tags: ["e2e", "microflow"],
          parameters: [{ id: "p1", stableId: "p1", name: "amount", dataType: { kind: "decimal" }, required: true }],
          returnType: { kind: "boolean" },
          returnVariableName: "isHigh",
          template: "blank"
        }
      }
    });
    expect(createValidate.ok()).toBeTruthy();
    const validatePayload = await createValidate.json();
    const validateId = String(validatePayload?.data?.id ?? "");
    expect(validateId).not.toBe("");

    const createSubmit = await request.post(`${appApiBase}/api/v1/microflows`, {
      headers,
      data: {
        workspaceId,
        input: {
          name: submitName,
          displayName: submitName,
          description: "e2e submit flow",
          moduleId,
          moduleName: moduleId,
          tags: ["e2e", "microflow"],
          parameters: [{ id: "p1", stableId: "p1", name: "amount", dataType: { kind: "decimal" }, required: true }],
          returnType: { kind: "boolean" },
          returnVariableName: "submitted",
          template: "blank"
        }
      }
    });
    expect(createSubmit.ok()).toBeTruthy();
    const submitPayload = await createSubmit.json();
    const submitId = String(submitPayload?.data?.id ?? "");
    expect(submitId).not.toBe("");

    const schemaResp = await request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/schema`, { headers });
    expect(schemaResp.ok()).toBeTruthy();
    const schemaPayload = await schemaResp.json();
    const baseSchema = schemaPayload?.data?.schema;
    const baseVersion = schemaPayload?.data?.schemaVersion;

    const brokenSave = await request.put(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/schema`, {
      headers,
      data: {
        schema: createBrokenPublishSchema(baseSchema),
        baseVersion,
        saveReason: "e2e-publish-blocked",
        clientRequestId: `e2e-publish-blocked-${now}`
      }
    });
    expect(brokenSave.ok()).toBeTruthy();

    const blockedPublish = await request.post(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/publish`, {
      headers,
      data: {
        version: `1.0.${now}`,
        description: "blocked publish should fail"
      }
    });
    expect(blockedPublish.status()).toBe(422);
    const blockedPayload = await blockedPublish.json();
    expect(blockedPayload?.error?.code ?? blockedPayload?.code).toBeTruthy();

    const brokenSchemaResp = await request.get(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/schema`, { headers });
    expect(brokenSchemaResp.ok()).toBeTruthy();
    const fixedSave = await request.put(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/schema`, {
      headers,
      data: {
        schema: createRunnableLogSchema(baseSchema, `Runtime trace ${submitName}`),
        baseVersion: (await brokenSchemaResp.json())?.data?.schemaVersion,
        saveReason: "e2e-publish-fix",
        clientRequestId: `e2e-publish-fix-${now}`,
        force: true
      }
    });
    expect(fixedSave.ok()).toBeTruthy();

    const publishResp = await request.post(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/publish`, {
      headers,
      data: {
        version: `1.1.${now}`,
        description: "e2e publish after validation fix",
        confirmBreakingChanges: true
      }
    });
    expect(publishResp.ok()).toBeTruthy();

    const runResp = await request.post(`${appApiBase}/api/v1/microflows/${encodeURIComponent(submitId)}/test-run`, {
      headers,
      data: {
        input: {
          amount: 120
        },
        options: {
          maxSteps: 200,
          allowRealHttp: false
        }
      }
    });
    expect(runResp.ok()).toBeTruthy();
    const runPayload = await runResp.json();
    const session = runPayload?.data?.session;
    expect(session).toBeTruthy();
    expect(["success", "failed", "unsupported", "cancelled"]).toContain(session.status);
    expect(Array.isArray(session.trace)).toBeTruthy();

    const traceHasFrames = Array.isArray(session.trace) && session.trace.length > 0;
    expect(traceHasFrames).toBeTruthy();
    expect(session.trace.some((frame: { nodeId?: string; nodeObjectId?: string }) =>
      frame.nodeObjectId === "e2e-runtime-log" || frame.nodeId === "e2e-runtime-log"
    )).toBeTruthy();
  });
});
