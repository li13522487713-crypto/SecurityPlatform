import { expect, test } from "../fixtures/single-session";
import {
  createWorkflowSession,
  expectWorkflowEditorReady,
  openWorkflowEditor
} from "./workflow-e2e-helpers";

/**
 * M1 Smoke：进入工作流编辑器后，确认 cozelib 的变量快照面板可被发现。
 *
 * 完整链路（拖拽 LLM 节点 → 变量绑定 → Prompt {{var}} 插入 → save → test_run → 调试快照）
 * 留给 M6 阶段编排。本里程碑只需保证：
 * - 编辑器能在 Atlas Foundation 桥接下顺利加载，不出现 useUserInfo() == null 崩溃；
 * - cozelib 内部的「调试 / Variable」入口控件能被检测到。
 */
test.describe.serial("@m1-smoke workflow variable snapshot", () => {
  test("workflow editor renders the variable / debug entry surfaces", async ({
    page,
    request,
    ensureLoggedInSession
  }, testInfo) => {
    test.setTimeout(180_000);

    const { appKey, workflowId } = await createWorkflowSession(page, request, ensureLoggedInSession);
    await openWorkflowEditor(page, appKey, workflowId);
    await expectWorkflowEditorReady(page);

    // 调试 / Variable 入口在不同模式下显示文案不同，统一断言至少出现一种。
    const debugEntry = page
      .locator("button, [role='button']")
      .filter({ hasText: /Variable \(Debug\)|变量|Debug|调试/ })
      .first();

    await expect(
      debugEntry,
      "M1 Smoke: 调试 / 变量入口未渲染，怀疑 atlas-foundation-bridge 注入未生效"
    ).toBeVisible({ timeout: 30_000 });

    await testInfo.attach("workflow-variable-snapshot.png", {
      body: await page.screenshot({ fullPage: true }),
      contentType: "image/png"
    });
  });
});
