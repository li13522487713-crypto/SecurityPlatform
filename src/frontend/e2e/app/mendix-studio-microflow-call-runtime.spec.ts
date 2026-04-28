import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup } from "./helpers";

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
      "X-Tenant-Id": "00000000-0000-0000-0000-000000000001",
      "X-Workspace-Id": workspaceId,
      "X-User-Id": "admin"
    };

    const now = Date.now();
    const validateName = `E2E_MF_VALIDATE_${now}`;
    const submitName = `E2E_MF_SUBMIT_${now}`;
    const moduleId = "sales";

    const createValidate = await request.post("http://127.0.0.1:5002/api/v1/microflows", {
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

    const createSubmit = await request.post("http://127.0.0.1:5002/api/v1/microflows", {
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

    const runResp = await request.post(`http://127.0.0.1:5002/api/v1/microflows/${encodeURIComponent(submitId)}/test-run`, {
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
  });
});
