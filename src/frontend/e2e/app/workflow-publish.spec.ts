import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession } from "./workflow-e2e-helpers";

test.describe.serial("Workflow Publish E2E", () => {
  test("should trigger publish request from editor header", async ({ page, request, ensureLoggedInSession }) => {
    const { workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);

    const saveDraftResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`);
    }, { timeout: 15_000 });
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.status()).toBe(200);

    const publishResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "POST" && /\/api\/v2\/workflows\/[^/]+\/publish$/.test(response.url());
    }, { timeout: 15_000 });

    await page.getByTestId("workflow-base-publish-button").click();
    const publishResponse = await publishResponsePromise;
    expect(publishResponse.status()).toBe(200);
    const publishPayload = (await publishResponse.json()) as { success?: boolean };
    expect(publishPayload.success).toBeTruthy();
  });
});

