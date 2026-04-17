import { expect, test, type APIRequestContext, type Page } from "../fixtures/single-session";
import {
  createWorkflowSession,
  expectWorkflowEditorReady,
  openWorkflowEditor
} from "./workflow-e2e-helpers";
import { captureEvidenceScreenshot } from "./helpers";

async function openFreshWorkflowEditor(
  page: Page,
  request: APIRequestContext,
  ensureLoggedInSession?: (appKey: string) => Promise<void>
) {
  const { appKey, workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
  await openWorkflowEditor(page, appKey, workflowId);
  await expectWorkflowEditorReady(page);
  return { workflowId };
}

test.describe.serial("Workflow editor layout", () => {
  test("should keep a non-zero canvas height on direct editor routes and after reload", async ({
    page,
    request,
    ensureLoggedInSession
  }, testInfo) => {
    test.setTimeout(240_000);

    const { workflowId } = await openFreshWorkflowEditor(page, request, ensureLoggedInSession);

    const verifyCanvasLayout = async () => {
      const metrics = await page.evaluate(() => {
        const workflowPlayground = document.getElementById("workflow-playground-content");
        const startNode = Array.from(document.querySelectorAll<HTMLElement>("*")).find(
          (element) => element.textContent?.trim() === "开始"
        );
        const zoomLabel = Array.from(document.querySelectorAll<HTMLElement>("*")).find((element) =>
          /%$/.test(element.textContent?.trim() ?? "")
        );

        return {
          workflowPlaygroundHeight: workflowPlayground?.getBoundingClientRect().height ?? 0,
          workflowPlaygroundWidth: workflowPlayground?.getBoundingClientRect().width ?? 0,
          startRect: startNode?.getBoundingClientRect().toJSON() ?? null,
          zoomText: zoomLabel?.textContent?.trim() ?? ""
        };
      });

      expect(metrics.workflowPlaygroundHeight, `workflow ${workflowId} canvas height collapsed`).toBeGreaterThan(300);
      expect(metrics.workflowPlaygroundWidth).toBeGreaterThan(300);
      expect(metrics.startRect).toBeTruthy();
      expect(metrics.startRect?.width ?? 0, "start node was not rendered at normal size").toBeGreaterThan(100);
      expect(metrics.startRect?.height ?? 0, "start node height is unexpectedly small").toBeGreaterThan(20);
      expect(metrics.zoomText, "viewport zoom is still stuck at the collapsed fallback value").not.toBe("10%");
    };

    await verifyCanvasLayout();
    await captureEvidenceScreenshot(page, testInfo, "workflow-editor-layout-before-reload");

    await page.reload({ waitUntil: "domcontentloaded" });
    await expectWorkflowEditorReady(page);
    await verifyCanvasLayout();
    await captureEvidenceScreenshot(page, testInfo, "workflow-editor-layout-after-reload");
  });
});
