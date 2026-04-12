import { expect, test, type Page } from "../fixtures/single-session";
import {
  clickWorkflowTestRun,
  connectWorkflowPorts,
  createWorkflowSession,
  dragNodeCatalogItemToCanvas,
  expectWorkflowEditorReady,
  hoverCanvasAt,
  humanHoverLocator,
  workflowConnectionLocator,
  workflowNodeLocator
} from "./workflow-e2e-helpers";

async function insertLlmNodeFromPanel(page: Page): Promise<void> {
  await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
  const searchInput = page.getByTestId("workflow.detail.node-panel.search");
  await searchInput.fill("llm");
  await expect(page.locator(".wf-react-node-item").first()).toBeVisible();
  await page.locator(".wf-react-node-item").first().click();
  await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();
}

test.describe.serial("Workflow Editor E2E", () => {
  test("should open workflow editor and show core controls", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await expectWorkflowEditorReady(page);

    await expect(page.getByTestId("workflow.detail.toolbar.add-node")).toBeVisible();
    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.node-panel.search")).toBeVisible();
    await page.getByTestId("workflow.detail.node-panel.search").fill("llm");
  });

  test("should add node from panel and keep editor usable after refresh", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowNodes = workflowNodeLocator(page);
    const starterNodeCount = await workflowNodes.count();

    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
    await insertLlmNodeFromPanel(page);
    await expect(workflowNodes).toHaveCount(starterNodeCount + 1);

    await page.reload();
    await page.waitForURL(/\/workflows\/[^/]+\/editor(?:\?.*)?$/, { timeout: 30_000 });
    await expect(page.getByTestId("workflow.detail.toolbar.add-node")).toBeVisible();
    await expect(workflowNodes.first()).toBeVisible();
  });

  test("should expose duplicate action in editor header", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);

    const duplicateButton = page.getByTestId("workflow.detail.title.duplicate");
    await expect(duplicateButton).toBeVisible();
    await expect(duplicateButton).toBeEnabled();

    await duplicateButton.click();
    await expect(page.locator(".wf-react-canvas-shell")).toBeVisible();
  });

  test("should click test-run panel run button from workflow editor", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    await clickWorkflowTestRun(page);
    await expect
      .poll(
        async () => {
          const items = await page.getByTestId("workflow.detail.node.testrun.result-item").allTextContents();
          return items.join("\n");
        },
        { timeout: 30_000 }
      )
      .toContain("execution");
  });

  test("should insert node from line add button", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowNodes = workflowNodeLocator(page);
    const starterNodeCount = await workflowNodes.count();

    await hoverCanvasAt(page, { x: 570, y: 200 });
    const lineAddButton = page.locator(".wf-react-line-add-btn").first();
    await expect(lineAddButton).toBeVisible();
    await lineAddButton.click();
    await insertLlmNodeFromPanel(page);
    await expect(workflowNodes).toHaveCount(starterNodeCount + 1);
    await hoverCanvasAt(page, { x: 800, y: 200 });
    await expect(page.locator(".wf-react-line-add-btn").first()).toBeVisible();
  });

  test("should drag node from panel to canvas with human-like motion", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowNodes = workflowNodeLocator(page);
    const starterNodeCount = await workflowNodes.count();

    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await dragNodeCatalogItemToCanvas(page, "llm", { x: 620, y: 260 });

    await expect(workflowNodes).toHaveCount(starterNodeCount + 1);
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();
  });

  test("should connect nodes by dragging ports with human-like motion", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    const initialNodeCount = await workflowNodeLocator(page).count();
    const initialConnectionCount = await workflowConnectionLocator(page).count();

    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await dragNodeCatalogItemToCanvas(page, "llm", { x: 620, y: 280 });
    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await dragNodeCatalogItemToCanvas(page, "llm", { x: 980, y: 280 });

    const workflowNodes = workflowNodeLocator(page);
    await expect(workflowNodes).toHaveCount(initialNodeCount + 2);

    const connections = workflowConnectionLocator(page);
    await expect(connections).toHaveCount(initialConnectionCount);

    const llmNodes = workflowNodes.filter({ hasText: /大模型|Llm|LLM/i });
    await expect(llmNodes).toHaveCount(2);

    const sourceNode = llmNodes.first();
    const targetNode = llmNodes.last();
    const sourcePort = sourceNode.locator('[data-wf-port="true"][data-port-kind="output"]').first();
    const targetPort = targetNode.locator('[data-wf-port="true"][data-port-kind="input"]').first();

    await humanHoverLocator(page, sourcePort);
    await connectWorkflowPorts(page, sourcePort, targetPort);

    await expect(connections).toHaveCount(initialConnectionCount + 1);
  });
});
