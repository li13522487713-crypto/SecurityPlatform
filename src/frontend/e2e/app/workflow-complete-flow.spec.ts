import { expect, test, type ConsoleMessage } from "../fixtures/single-session";
import { clickWorkflowTestRun, createWorkflowSession, openWorkflowEditor } from "./workflow-e2e-helpers";

function stringifyConsoleMessage(message: ConsoleMessage): string {
  return `${message.type()}: ${message.text()}`.trim();
}

function isUnexpectedConsoleError(entry: string) {
  return /error:/i.test(entry) &&
    !entry.includes("status of 400 (Bad Request)") &&
    !entry.includes("Failed to load resource");
}

function parseCanvasValue(raw: string) {
  return JSON.parse(raw) as {
    nodes?: Array<{ key: string; type: string; title: string; configs?: Record<string, unknown> }>;
    connections?: Array<{ fromNode: string; fromPort: string; toNode: string; toPort: string }>;
  };
}

test.describe.serial("Workflow Complete Flow", () => {
  // Coze playground 接管后未发出 workflow.detail.title.save-draft / canvas-json 等 testId；
  // 详见 docs/e2e-baseline-failures.md §3。整链路 case 暂以 fixme 记录，等待 spec 重写为 Coze 钩子。
  test.fixme("应完成应用端工作流中文全链路（新建、命名、保存、发布、测试运行、回列表再进入）", async ({
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
    const initialCanvas = parseCanvasValue(await page.getByTestId("workflow.detail.canvas-json").inputValue());

    await page.getByTestId("workflow.detail.meta.name").fill(workflowName);

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
    expect(publishResponse.status()).toBe(200);
    const publishPayload = (await publishResponse.json()) as { success?: boolean };
    expect(publishPayload.success).toBeTruthy();

    await clickWorkflowTestRun(page, "{\"input\":\"hello\"}");
    await expect(page.getByTestId("workflow.detail.node.testrun.result-panel")).toBeVisible();

    await page.getByTestId("workflow.detail.title.back").click();
    await expect(page).toHaveURL(/\/org\/[^/]+\/workspaces\/[^/]+\/workflows(?:\?.*)?$/, {
      timeout: 30_000
    });
    await expect(page.getByTestId("app-develop-page")).toBeVisible({ timeout: 30_000 });

    await openWorkflowEditor(page, appKey, workflowId);
    const reopenedCanvas = parseCanvasValue(await page.getByTestId("workflow.detail.canvas-json").inputValue());
    expect((reopenedCanvas.nodes ?? []).length).toBeGreaterThan(0);
    expect((reopenedCanvas.connections ?? []).length).toBe((initialCanvas.connections ?? []).length);

    expect(pageErrors, `检测到页面运行时异常: ${pageErrors.join("\n")}`).toEqual([]);
    expect(consoleEvents.filter(isUnexpectedConsoleError), `检测到异常控制台输出: ${consoleEvents.join("\n")}`).toEqual([]);
  });
});
