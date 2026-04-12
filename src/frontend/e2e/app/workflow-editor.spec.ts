import { expect, test } from "../fixtures/single-session";
import { createWorkflowSession, openWorkflowEditor } from "./workflow-e2e-helpers";

test.describe.serial("Workflow Editor E2E", () => {
  test("should open workflow editor and show core controls", async ({ page, request }) => {
    page.on("console", (msg) => console.log("BROWSER CONSOLE:", msg.type(), msg.text()));
    page.on("pageerror", (error) => console.log("BROWSER PAGE ERROR:", error.message));
    await createWorkflowSession(page, request);

    await expect(page.getByTestId("workflow.detail.toolbar.add-node")).toBeVisible();
    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
    await expect(page.getByTestId("workflow.detail.node-panel.search")).toBeVisible();
    await page.getByTestId("workflow.detail.node-panel.search").fill("llm");
  });

  test("should add node from panel and keep editor usable after refresh", async ({ page, request }) => {
    const { appKey, workflowId } = await createWorkflowSession(page, request);

    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();
    await expect(page.locator(".wf-react-node-item").first()).toBeVisible();
    await page.locator(".wf-react-node-item").first().click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();

    await page.reload();
    await openWorkflowEditor(page, appKey, workflowId);
    await expect(page.getByTestId("workflow.detail.toolbar.add-node")).toBeVisible();
  });

  test("should expose duplicate action in editor header", async ({ page, request }) => {
    await createWorkflowSession(page, request);

    const duplicateButton = page.getByTestId("workflow.detail.title.duplicate");
    await expect(duplicateButton).toBeVisible();
    await expect(duplicateButton).toBeEnabled();

    await page.getByTestId("workflow.detail.title.duplicate").click();
    await expect(page.locator(".wf-react-canvas-shell")).toBeVisible();
  });

  test("should insert node from line add button", async ({ page, request }) => {
    await createWorkflowSession(page, request);

    const canvas = page.locator(".wf-react-canvas-shell");
    const box = await canvas.boundingBox();
    expect(box).toBeTruthy();
    await page.mouse.move((box?.x ?? 0) + 570, (box?.y ?? 0) + 200);

    const lineAddButton = page.locator(".wf-react-line-add-btn").first();
    await expect(lineAddButton).toBeVisible();
    await lineAddButton.click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();

    await page.locator(".wf-react-node-item").first().click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();
    await page.mouse.move((box?.x ?? 0) + 800, (box?.y ?? 0) + 200);
    await expect(page.locator(".wf-react-line-add-btn").first()).toBeVisible();
  });

  test("should insert node from output port click", async ({ page, request }) => {
    await createWorkflowSession(page, request);

    // Insert a node first so we have something to click
    await page.getByTestId("workflow.detail.toolbar.add-node").click();
    await page.locator(".wf-react-node-item").first().click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();

    const nodeCards = page.locator(".wf-node-render-shell");
    const beforeNodeCount = await nodeCards.count();
    expect(beforeNodeCount).toBeGreaterThan(0);

    const firstNode = nodeCards.first();
    const box = await firstNode.boundingBox();
    expect(box).toBeTruthy();

    await page.mouse.click((box?.x ?? 0) + (box?.width ?? 0) - 4, (box?.y ?? 0) + (box?.height ?? 0) / 2);
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeVisible();

    await page.locator(".wf-react-node-item").first().click();
    await expect(page.getByTestId("workflow.detail.node-panel")).toBeHidden();
    await expect(nodeCards).toHaveCount(beforeNodeCount + 1);
  });
});
