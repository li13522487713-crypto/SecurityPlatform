import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession } from "./workflow-e2e-helpers";
import { appBaseUrl } from "./helpers";

test.describe.serial("Workflow Collaboration E2E", () => {
  test("should open same workflow in two tabs", async ({ page, request, context }) => {
    const { appKey, workflowId } = await createWorkflowSession(page, request);

    const secondTab = await context.newPage();
    await secondTab.goto(
      `${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(workflowId)}/editor`
    );
    await secondTab.waitForURL(
      new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(workflowId)}/editor(?:\\?.*)?$`),
      { timeout: 30_000 }
    );
    await expect(secondTab.locator(".wf-react-canvas-shell")).toBeVisible();
    await secondTab.close();
  });
});

