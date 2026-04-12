import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession, openWorkflowEditor } from "./workflow-e2e-helpers";
import { loginApp } from "./helpers";

test.describe.serial("Workflow Collaboration E2E", () => {
  test("should open same workflow in two tabs", async ({ page, request, context, ensureLoggedInSession }) => {
    const { appKey, workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);

    const secondTab = await context.newPage();
    await loginApp(secondTab, appKey);
    await openWorkflowEditor(secondTab, appKey, workflowId);
    await expect(secondTab.locator(".wf-react-canvas-shell")).toBeVisible();
    await secondTab.close();
  });
});

