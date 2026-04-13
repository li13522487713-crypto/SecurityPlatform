import { expect, test, type ConsoleMessage } from "../fixtures/single-session";
import { clickWorkflowTestRun, createWorkflowSession, openWorkflowEditor } from "./workflow-e2e-helpers";

function stringifyConsoleMessage(message: ConsoleMessage): string {
  return `${message.type()}: ${message.text()}`.trim();
}

function isUnexpectedConsoleError(entry: string) {
  return /error:/i.test(entry) && !entry.includes("status of 400 (Bad Request)");
}

function parseCanvasValue(raw: string) {
  return JSON.parse(raw) as {
    nodes?: Array<{ key: string; type: string; title: string; configs?: Record<string, unknown> }>;
    connections?: Array<{ from: string; to: string }>;
  };
}

test.describe.serial("Workflow Complete Flow", () => {
  test("应完成应用端工作流中文全链路（新建、命名、保存、发布、测试运行、回列表再进入）", async ({
    page,
    request,
    ensureLoggedInSession
  }) => {
    test.setTimeout(240_000);

    const consoleEvents: string[] = [];
    const pageErrors: string[] = [];

    page.on("console", (message) => {
      consoleEvents.push(stringifyConsoleMessage(message));
    });
    page.on("pageerror", (error) => {
      pageErrors.push(error.message);
    });

    const { appKey, workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowName = `中文流程_${Date.now().toString().slice(-6)}`;
    const canvasJson = JSON.stringify({
      nodes: [
        { key: "start_node", type: "start", title: "开始" },
        { key: "summary_node", type: "llm", title: "审批总结" }
      ],
      connections: [
        { from: "start_node", to: "summary_node" }
      ]
    }, null, 2);

    await page.getByTestId("workflow.detail.meta.name").fill(workflowName);
    await page.getByTestId("workflow.detail.meta.description").fill("应用端中文工作流全链路测试");
    await page.getByTestId("workflow.detail.canvas-json").fill(canvasJson);

    const saveDraftResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`);
    }, { timeout: 30_000 });
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.ok()).toBeTruthy();

    const publishResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "POST" && response.url().endsWith(`/api/v2/workflows/${workflowId}/publish`);
    }, { timeout: 30_000 });
    await page.getByTestId("workflow-base-publish-button").click();
    const publishResponse = await publishResponsePromise;
    expect([200, 400]).toContain(publishResponse.status());

    await clickWorkflowTestRun(page, "{\"input\":\"hello\"}");
    await expect(page.getByTestId("workflow.detail.node.testrun.result-panel")).toBeVisible();

    await page.getByTestId("workflow.detail.title.back").click();
    await expect(page).toHaveURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/work_flow(?:\\?.*)?$`), {
      timeout: 30_000
    });
    await expect(page.getByTestId("app-workflows-page")).toBeVisible({ timeout: 30_000 });

    await openWorkflowEditor(page, appKey, workflowId);
    const reopenedCanvas = parseCanvasValue(await page.getByTestId("workflow.detail.canvas-json").inputValue());
    expect(reopenedCanvas.nodes).toEqual([
      { key: "start_node", type: "start", title: "开始", configs: {} },
      { key: "summary_node", type: "llm", title: "审批总结", configs: {} }
    ]);
    expect(reopenedCanvas.connections).toEqual([
      { from: "start_node", to: "summary_node" }
    ]);

    expect(pageErrors, `检测到页面运行时异常: ${pageErrors.join("\n")}`).toEqual([]);
    expect(consoleEvents.filter(isUnexpectedConsoleError), `检测到异常控制台输出: ${consoleEvents.join("\n")}`).toEqual([]);
  });
});
