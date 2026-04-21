import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  ensureAppSetup,
  navigateBySidebar
} from "./helpers";

test.describe.serial("App Visualization And Runtime", () => {
  test.fixme("旧壳可视化与 runtime 入口已下线，待新壳运行时入口场景补齐后恢复。");
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("visualization page should load", async ({ page }) => {
    await navigateBySidebar(page, "visualization", {
      pageTestId: "app-visualization-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/visualization(?:\\?.*)?$`)
    });
    await expect(page.getByTestId("app-visualization-table")).toBeVisible();
  });

  test("entry page should resolve to runtime or show deterministic fallback", async ({ page }) => {
    test.setTimeout(90_000);
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/entry`);
    // 当前 IA：legacy /apps/<appKey>/entry 经 LegacyAppRedirectRoute 解析后无 entry 映射 →
    // 回落到工作空间 dashboard（app-dashboard-page）。三种合法的稳态都接受：
    //   1) /r/ 运行时路由（保留旧行为）
    //   2) 显示 entry-gateway-warning 显式提示
    //   3) 已被重定向到工作空间 dashboard
    await expect.poll(
      async () => {
        const currentUrl = page.url();
        if (/\/apps\/[^/]+\/r\/[^/]+/.test(currentUrl)) {
          return "runtime";
        }

        const warningResult = await page.getByTestId("app-entry-gateway-warning").isVisible().catch(() => false);
        if (warningResult) {
          return "fallback";
        }

        const dashboardResult = await page.getByTestId("app-dashboard-page").isVisible().catch(() => false);
        if (dashboardResult) {
          return "dashboard";
        }

        return "pending";
      },
      { timeout: 60_000 }
    ).not.toBe("pending");

    const url = page.url();
    if (/\/apps\/[^/]+\/r\/[^/]+/.test(url)) {
      await expect(page.getByTestId("app-runtime-page")).toBeVisible();
    } else if (await page.getByTestId("app-entry-gateway-warning").isVisible().catch(() => false)) {
      await expect(page.getByTestId("app-entry-gateway-warning")).toBeVisible();
    } else {
      await expect(page.getByTestId("app-dashboard-page")).toBeVisible();
    }
  });
});

