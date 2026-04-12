import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession, expectWorkflowEditorReady } from "./workflow-e2e-helpers";

function parseCanvasValue(raw: string) {
  return JSON.parse(raw) as {
    nodes?: Array<{ key: string; type: string; title: string; configs?: Record<string, unknown> }>;
    connections?: Array<{ from: string; to: string }>;
  };
}

test.describe.serial("Workflow Editor E2E", () => {
  test("should open workflow editor and show core controls", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await expectWorkflowEditorReady(page);

    await expect(page.getByTestId("workflow.detail.title.save-draft")).toBeVisible();
    await expect(page.getByTestId("workflow-base-publish-button")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.toolbar.test-run")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.canvas-json")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.run-inputs")).toBeVisible();
  });

  test("should save edited canvas json and keep editor usable after refresh", async ({ page, request, ensureLoggedInSession }) => {
    const { workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
    const canvasEditor = page.getByTestId("workflow.detail.canvas-json");
    const nextCanvas = JSON.stringify({
      nodes: [
        { key: `start_${workflowId}`, type: "start", title: "开始节点" },
        { key: `llm_${workflowId}`, type: "llm", title: "审批总结" }
      ],
      connections: [
        { from: `start_${workflowId}`, to: `llm_${workflowId}` }
      ]
    }, null, 2);

    await canvasEditor.fill(nextCanvas);

    const saveDraftResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`);
    }, { timeout: 30_000 });
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.ok()).toBeTruthy();

    await page.reload();
    await page.waitForURL(/\/work_flow\/[^/]+\/editor(?:\?.*)?$/, { timeout: 30_000 });
    const reopenedCanvas = parseCanvasValue(await page.getByTestId("workflow.detail.canvas-json").inputValue());
    expect(reopenedCanvas.nodes).toEqual([
      { key: `start_${workflowId}`, type: "start", title: "开始节点", configs: {} },
      { key: `llm_${workflowId}`, type: "llm", title: "审批总结", configs: {} }
    ]);
    expect(reopenedCanvas.connections).toEqual([
      { from: `start_${workflowId}`, to: `llm_${workflowId}` }
    ]);
  });

  test("should expose duplicate action and allow returning to list", async ({ page, request, ensureLoggedInSession }) => {
    const { appKey } = await createWorkflowSession(page, request, ensureLoggedInSession);

    await expect(page.getByTestId("workflow.detail.title.duplicate")).toBeVisible();
    await page.getByTestId("workflow.detail.title.duplicate").click();
    await expect(page.getByTestId("app-workflow-editor-shell")).toBeVisible();

    await page.getByTestId("workflow.detail.title.back").click();
    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/work_flow(?:\\?.*)?$`), {
      timeout: 30_000
    });
    await expect(page.getByTestId("app-workflows-page")).toBeVisible();
  });
});
