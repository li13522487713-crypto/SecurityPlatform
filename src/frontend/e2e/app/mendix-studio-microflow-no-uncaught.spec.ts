import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup, ensureAppWorkspace } from "./helpers";

test.describe.serial("@microflow Mendix Studio no uncaught", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("进入 Mendix Studio 全程无 uncaught/console error", async ({ page }, testInfo) => {
    const consoleErrors: string[] = [];
    const pageErrors: string[] = [];
    const ignoredPatterns = [
      /ResizeObserver loop limit exceeded/i,
      /favicon.ico/i,
      /Failed to load resource: the server responded with a status of 404/i,
    ];

    const onConsole = (message: import("@playwright/test").ConsoleMessage) => {
      if (message.type() !== "error") {
        return;
      }
      const text = message.text();
      if (ignoredPatterns.some(pattern => pattern.test(text))) {
        return;
      }
      consoleErrors.push(text);
    };
    const onPageError = (error: Error) => {
      const text = error.message;
      if (ignoredPatterns.some(pattern => pattern.test(text))) {
        return;
      }
      pageErrors.push(text);
    };

    page.on("console", onConsole);
    page.on("pageerror", onPageError);

    try {
      await ensureAppWorkspace(page, appKey);

      const workspaceMatch = page.url().match(/\/workspace\/([^/]+)/);
      expect(workspaceMatch?.[1]).toBeTruthy();
      const workspaceId = workspaceMatch![1];
      await page.goto(`${appBaseUrl}/space/${encodeURIComponent(workspaceId)}/mendix-studio/${encodeURIComponent(appKey)}`, {
        waitUntil: "domcontentloaded",
      });
      await page.waitForLoadState("networkidle");

      await expect(page).toHaveURL(new RegExp(`/space/${workspaceId}/mendix-studio/${appKey}(?:\\?.*)?$`));
      await expect(page.locator(".mendix-studio-root")).toBeVisible();
      await expect(pageErrors, `pageerror: ${pageErrors.join("\n")}`).toEqual([]);
      await expect(consoleErrors, `console error: ${consoleErrors.join("\n")}`).toEqual([]);
    } finally {
      page.off("console", onConsole);
      page.off("pageerror", onPageError);
      await testInfo.attach("mendix-studio-errors", {
        body: JSON.stringify({ consoleErrors, pageErrors }, null, 2),
        contentType: "application/json",
      });
    }
  });
});
