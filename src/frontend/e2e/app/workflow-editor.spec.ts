import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession, expectWorkflowEditorReady } from "./workflow-e2e-helpers";

function parseCanvasValue(raw: string) {
  return JSON.parse(raw) as {
    nodes?: Array<{ key: string; type: string; title: string; configs?: Record<string, unknown> }>;
    connections?: Array<{ fromNode: string; fromPort: string; toNode: string; toPort: string }>;
  };
}

test.describe.serial("Workflow Editor E2E", () => {
  // 当前工作流编辑器已切换到 Coze playground（@coze-workflow/playground-adapter），
  // 它不再渲染 Atlas 旧版的 workflow.detail.title.save-draft / workflow.detail.canvas-json
  // 等 testId（Coze 改为自动保存 + 内置画布，无 textarea 形态）。
  // 详见 docs/e2e-baseline-failures.md §3 「workflow-* 系列」与 §4「专项二」。
  // 在这些 testId 由 packages/workflow 内补回 / spec 重写为 Coze 原生钩子之前，
  // 暂以 fixme 标记，避免 ordered run 中产生级联失败。
  test.fixme("should open workflow editor and show core controls", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await expectWorkflowEditorReady(page);

    await expect(page.getByTestId("workflow.detail.title.save-draft")).toBeVisible();
    await expect(page.getByTestId("workflow-base-publish-button")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.toolbar.test-run")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.canvas-json")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.run-inputs")).toBeVisible();
  });

  test.fixme("should save edited canvas json and keep editor usable after refresh", async ({ page, request, ensureLoggedInSession }) => {
    const { workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
    const canvasEditor = page.getByTestId("workflow.detail.canvas-json");
    const currentCanvas = parseCanvasValue(await canvasEditor.inputValue());
    const nextCanvas = JSON.stringify({
      ...currentCanvas,
      nodes: (currentCanvas.nodes ?? []).map((node, index) => ({
        ...node,
        title: index === 0 ? `开始节点_${workflowId}` : `结束节点_${workflowId}`
      }))
    }, null, 2);

    await canvasEditor.fill(nextCanvas);

    const saveDraftResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`);
    }, { timeout: 30_000 });
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.ok()).toBeTruthy();

    await page.reload();
    // 当前路径形态为 /org/<org>/workspaces/<ws>/workflows/<workflowId>，旧 /work_flow/<id>/editor 已淘汰。
    await page.waitForURL(/\/(?:work_flow\/[^/]+\/editor|workflows\/[^/?#]+)(?:\?.*)?$/, { timeout: 30_000 });
    await expectWorkflowEditorReady(page);
    await expect(page.getByTestId("workflow.detail.canvas-json")).toBeVisible();
    const reopenedCanvas = parseCanvasValue(await page.getByTestId("workflow.detail.canvas-json").inputValue());
    expect((reopenedCanvas.nodes ?? []).length).toBe((currentCanvas.nodes ?? []).length);
    expect((reopenedCanvas.connections ?? []).length).toBe((currentCanvas.connections ?? []).length);
  });

  test.fixme("should expose duplicate action and allow returning to list", async ({ page, request, ensureLoggedInSession }) => {
    void (await createWorkflowSession(page, request, ensureLoggedInSession));

    await expect(page.getByTestId("workflow.detail.title.duplicate")).toBeVisible();
    await page.getByTestId("workflow.detail.title.duplicate").click();
    await expect(page.getByTestId("app-workflow-editor-shell")).toBeVisible();

    await page.getByTestId("workflow.detail.title.back").click();
    // 当前 IA：返回列表落到 /org/<org>/workspaces/<ws>/workflows
    await page.waitForURL(/\/org\/[^/]+\/workspaces\/[^/]+\/workflows(?:\?.*)?$/, {
      timeout: 30_000
    });
    // 列表页实际由 WorkspaceStudioRoute(focus=workflow) 渲染 → testId=app-develop-page
    await expect(page.getByTestId("app-develop-page")).toBeVisible();
  });
});
