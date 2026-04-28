import { expect, test } from "../fixtures/single-session";
import type { ConsoleMessage, Page } from "@playwright/test";
import { ensureAppSetup, ensureAppWorkspace } from "./helpers";
const reactDomWarningPatterns = [
  /non-boolean attribute `_show`/i,
  /non-boolean attribute `_selected`/i,
  /does not recognize the `_scrollIndex` prop/i,
  /non-boolean attribute `hide_operation`/i,
  /does not recognize the `_parentGroup` prop/i,
  /does not recognize the `_keyInJsx` prop/i,
  /scheduled from inside an update function/i
];

function attachConsoleCollector(page: Page, matchedWarnings: string[]) {
  const handleConsoleMessage = (message: ConsoleMessage) => {
    if (message.type() !== "warning" && message.type() !== "error") {
      return;
    }

    const text = message.text();
    if (reactDomWarningPatterns.some((pattern) => pattern.test(text))) {
      matchedWarnings.push(text);
    }
  };

  page.on("console", handleConsoleMessage);
  return () => page.off("console", handleConsoleMessage);
}

test.describe.serial("@smoke Workspace Switch Console", () => {
  test.setTimeout(180_000);

  test("展开并切换工作空间时不应出现 React DOM 属性告警", async ({ page, request, ensureLoggedInSession: ensureSession }) => {
    const appKey = await ensureAppSetup(request);
    await ensureSession(appKey);
    const matchedWarnings: string[] = [];
    const detachConsoleCollector = attachConsoleCollector(page, matchedWarnings);

    try {
      await ensureAppWorkspace(page, appKey);
      await page.getByTestId("coze-workspace-switcher-trigger").click();
      const optionLocator = page.locator('[data-testid^="coze-workspace-switcher-item-"]').first();
      await expect(optionLocator).toBeVisible({ timeout: 30_000 });
      await page.waitForTimeout(1200);

      expect(
        matchedWarnings,
        `检测到工作空间切换器相关控制台告警:\n${matchedWarnings.join("\n\n")}`
      ).toEqual([]);
    } finally {
      detachConsoleCollector();
    }
  });
});
