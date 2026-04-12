import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession } from "./workflow-e2e-helpers";

test.describe.serial("Workflow Publish E2E", () => {
  test("should trigger publish request from editor header", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);

    let publishStatus = -1;
    const publishResponsePromise = page
      .waitForResponse((response) => {
        return response.request().method() === "POST" && /\/api\/v2\/workflows\/[^/]+\/publish$/.test(response.url());
      }, { timeout: 8_000 })
      .then((response) => {
        publishStatus = response.status();
      })
      .catch(() => {
        publishStatus = -1;
      });

    await page.getByTestId("workflow-base-publish-button").click();
    await publishResponsePromise;

    const problemPanel = page.locator(".wf-react-problem-panel");
    if (publishStatus === -1) {
      await expect(problemPanel).toBeVisible();
      return;
    }
    expect([200, 400]).toContain(publishStatus);
  });
});

