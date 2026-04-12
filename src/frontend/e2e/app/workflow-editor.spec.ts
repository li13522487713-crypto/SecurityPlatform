import { expect, test, type Page } from "../fixtures/single-session";
import {
  createWorkflowSession,
  expectWorkflowEditorReady,
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

function workflowOutputPortLocator(page: Page) {
  return page.locator(".workflow-port-render[data-port-entity-type='output']").first();
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

  test("should insert node from line add button", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);
    const workflowNodes = workflowNodeLocator(page);
    const starterNodeCount = await workflowNodes.count();

    const canvas = page.locator(".wf-react-canvas-shell");
    const box = await canvas.boundingBox();
    expect(box).toBeTruthy();
    await page.mouse.move((box?.x ?? 0) + 570, (box?.y ?? 0) + 200);

    const lineAddButton = page.locator(".wf-react-line-add-btn").first();
    await expect(lineAddButton).toBeVisible();
    await lineAddButton.click();
    await insertLlmNodeFromPanel(page);
    await expect(workflowNodes).toHaveCount(starterNodeCount + 1);
    await page.mouse.move((box?.x ?? 0) + 800, (box?.y ?? 0) + 200);
    await expect(page.locator(".wf-react-line-add-btn").first()).toBeVisible();
  });

  test("should insert node from output port click", async ({ page, request, ensureLoggedInSession }) => {
    await createWorkflowSession(page, request, ensureLoggedInSession);

    const workflowNodes = workflowNodeLocator(page);
    const beforeNodeCount = await workflowNodes.count();
    expect(beforeNodeCount).toBeGreaterThan(0);

    const outputPort = workflowOutputPortLocator(page);
    await expect(outputPort).toBeVisible();
    await outputPort.click({ force: true });
    await insertLlmNodeFromPanel(page);
    await expect(workflowNodes).toHaveCount(beforeNodeCount + 1);
  });
});
