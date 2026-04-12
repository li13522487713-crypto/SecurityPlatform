import { expect, test, type ConsoleMessage, type Page } from "../fixtures/single-session";
import { createWorkflowSession, workflowNodeLocator } from "./workflow-e2e-helpers";

const BENIGN_CONSOLE_PATTERNS = [/ResizeObserver loop completed with undelivered notifications/i, /ResizeObserver loop limit exceeded/i];
const BLOCKING_CONSOLE_PATTERNS = [/Unhandled rejection/i, /ResizeObserver/i];

async function insertNodeFromPanel(page: Page, keywords: string[]): Promise<void> {
  await page.getByTestId("workflow.detail.toolbar.add-node").click();
  await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
  const searchInput = page.getByTestId("workflow.detail.node-panel.search");
  const firstNodeItem = page.locator(".wf-react-node-item").first();
  let foundKeyword = "";
  for (const keyword of keywords) {
    await searchInput.fill(keyword);
    const visible = await firstNodeItem.isVisible({ timeout: 2_000 }).catch(() => false);
    if (visible) {
      foundKeyword = keyword;
      break;
    }
  }
  expect(foundKeyword, `节点面板未能命中任一关键字: ${keywords.join(", ")}`).not.toBe("");
  await expect(searchInput).toHaveValue(foundKeyword);
  await page.locator(".wf-react-node-item").first().click();
  await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();
}

function isBenignConsoleMessage(text: string): boolean {
  return BENIGN_CONSOLE_PATTERNS.some((pattern) => pattern.test(text));
}

function isBlockingConsoleMessage(text: string): boolean {
  return BLOCKING_CONSOLE_PATTERNS.some((pattern) => pattern.test(text));
}

function stringifyConsoleMessage(message: ConsoleMessage): string {
  return `${message.type()}: ${message.text()}`.trim();
}

test.describe.serial("Workflow Complete Flow", () => {
  test("应完成应用端工作流中文全链路（新建、命名、编排、保存、发布、测试运行）", async ({
    page,
    request,
    ensureLoggedInSession
  }) => {
    test.setTimeout(360_000);

    const consoleEvents: string[] = [];
    const pageErrors: string[] = [];

    page.on("console", (message) => {
      const text = stringifyConsoleMessage(message);
      if (!isBenignConsoleMessage(text)) {
        consoleEvents.push(text);
      }
    });
    page.on("pageerror", (error) => {
      pageErrors.push(error.message);
    });

    const { workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowNodes = workflowNodeLocator(page);
    const starterNodeCount = await workflowNodes.count();
    expect(starterNodeCount).toBeGreaterThan(0);

    const workflowName = page.locator(".wf-react-name");
    await expect(workflowName).toBeVisible();
    await workflowName.fill("中文流程审批测试");
    await expect(workflowName).toHaveValue("中文流程审批测试");

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

    await page.getByTestId("workflow.detail.toolbar.test-run").click();
    const testRunPanel = page.getByTestId("workflow.detail.node.testrun.result-panel");
    await expect(testRunPanel).toBeVisible();

    await testRunPanel.locator("textarea").first().fill("{}");
    await testRunPanel.locator(".ant-btn-primary").first().click();

    await expect
      .poll(
        async () => {
          if ((await page.getByTestId("workflow.detail.node.testrun.result-item").count()) > 0) {
            return "result";
          }
          if (await page.locator(".wf-react-problem-panel").isVisible()) {
            return "problem";
          }
          return "pending";
        },
        { timeout: 30_000 }
      )
      .not.toBe("pending");

    await insertNodeFromPanel(page, ["大模型", "llm", "LLM"]);
    await expect(workflowNodes).toHaveCount(starterNodeCount + 1);

    const llmNode = workflowNodeLocator(page).filter({ hasText: /大模型|LLM/i }).last();
    await llmNode.click();
    await expect(page.locator(".wf-react-properties-panel")).toBeVisible();
    const titleInput = page.locator(".wf-react-properties-panel input").first();
    await titleInput.fill("中文审批节点");
    await expect(titleInput).toHaveValue("中文审批节点");

    await page.getByTestId("workflow.detail.toolbar.variables").click();
    await expect(page.locator(".wf-react-variable-panel")).toBeVisible();
    await page.locator(".wf-react-global-create .wf-react-global-key").fill("approvalLevel");
    await page.locator(".wf-react-global-create .wf-react-global-value").fill("\"高级审批\"");
    await page.locator(".wf-react-global-create button").click();
    await expect(page.locator(".wf-react-global-list .wf-react-global-key").first()).toHaveValue("approvalLevel");
    await page.locator(".wf-react-variable-panel .wf-react-variable-panel-header button").click();

    const draftAfterEditResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && response.url().endsWith(`/api/v2/workflows/${workflowId}/draft`);
    }, { timeout: 30_000 });
    await page.getByTestId("workflow.detail.title.save-draft").click();
    const draftAfterEditResponse = await draftAfterEditResponsePromise;
    expect(draftAfterEditResponse.ok()).toBeTruthy();

    await page.getByTestId("workflow.detail.toolbar.trace").click();
    await expect(page.locator(".wf-react-trace-panel")).toBeVisible();

    const blockingConsoleEvents = consoleEvents.filter((text) => isBlockingConsoleMessage(text));
    expect(blockingConsoleEvents, `检测到异常控制台输出: ${blockingConsoleEvents.join("\n")}`).toEqual([]);
    expect(pageErrors, `检测到页面运行时异常: ${pageErrors.join("\n")}`).toEqual([]);
  });
});
