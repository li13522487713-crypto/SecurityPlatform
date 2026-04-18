import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession, openWorkflowEditor } from "./workflow-e2e-helpers";
import { loginApp } from "./helpers";

test.describe.serial("Workflow Collaboration E2E", () => {
  // Coze playground 接管后未发出 app-workflow-editor-shell testId；详见 docs/e2e-baseline-failures.md。
  test.fixme("should open same workflow in two tabs", async ({ page, request, context, ensureLoggedInSession }) => {
    const { appKey, workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);

    const secondTab = await context.newPage();
    await loginApp(secondTab, appKey);
    await openWorkflowEditor(secondTab, appKey, workflowId);
    await expect(secondTab.getByTestId("app-workflow-editor-shell")).toBeVisible();
    await secondTab.close();
  });
});

