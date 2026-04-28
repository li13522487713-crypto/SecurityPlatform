import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup } from "./helpers";

test.describe.serial("Mendix Studio Microflow references delete", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("delete referenced microflow should be blocked", async ({ page, request, ensureLoggedInSession }) => {
    await ensureLoggedInSession(appKey);

    const createValidateResp = await request.post("http://127.0.0.1:5002/api/v1/microflows", {
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Id": "00000000-0000-0000-0000-000000000001",
      },
      data: {
        workspaceId: "atlas-space",
        input: {
          name: `E2E_MF_VALIDATE_REF_${Date.now()}`,
          displayName: "E2E Validate Ref",
          description: "E2E reference target",
          moduleId: "procurement",
          moduleName: "Procurement",
          tags: ["e2e", "mendix"],
          parameters: [],
          returnType: { kind: "boolean" },
          template: "blank",
        },
      },
    });
    expect(createValidateResp.ok()).toBeTruthy();
    const validatePayload = await createValidateResp.json();
    const validateId = String(validatePayload?.data?.id ?? "");
    expect(validateId).not.toBe("");

    const createSubmitResp = await request.post("http://127.0.0.1:5002/api/v1/microflows", {
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Id": "00000000-0000-0000-0000-000000000001",
      },
      data: {
        workspaceId: "atlas-space",
        input: {
          name: `E2E_MF_SUBMIT_REF_${Date.now()}`,
          displayName: "E2E Submit Ref",
          description: "E2E reference source",
          moduleId: "procurement",
          moduleName: "Procurement",
          tags: ["e2e", "mendix"],
          parameters: [],
          returnType: { kind: "boolean" },
          template: "blank",
        },
      },
    });
    expect(createSubmitResp.ok()).toBeTruthy();
    const submitPayload = await createSubmitResp.json();
    const submitId = String(submitPayload?.data?.id ?? "");
    expect(submitId).not.toBe("");

    // 通过发布动作附带引用信息，构造 Validate 被 Submit 引用的关系
    await request.post(`http://127.0.0.1:5002/api/v1/microflows/${encodeURIComponent(submitId)}/publish`, {
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Id": "00000000-0000-0000-0000-000000000001",
      },
      data: {
        version: "1.0.0",
        note: "e2e references setup",
        references: [{ targetMicroflowId: validateId }],
      },
    });

    await page.goto(`${appBaseUrl}/microflow`);
    await expect(page.getByText("新建微流").first()).toBeVisible({ timeout: 30_000 });

    await page.fill('input[placeholder="搜索微流"]', validateId);
    await page.waitForTimeout(300);
    await page.getByRole("button", { name: "删除" }).first().click();

    await expect(page.getByText(/删除被阻止|引用/)).toBeVisible({ timeout: 15_000 });
  });
});
