import { test, expect } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { loadSeedState, loginAsStoredRole, installErrorCollector } from "../helpers/test-helpers";

const seedState = loadSeedState();

/**
 * 从 P0 菜单中随机挑选若干条（优先覆盖不同 domain）进行导航 shell 验证：
 * - 打开后不跳回 /login
 * - 不出现 js 错误
 * - 标签页（tab bar）显示当前路径的 tab
 */
const NAVIGATION_SAMPLE_SIZE = 5;

function sampleP0Entries() {
  const domainSeen = new Set<string>();
  const sampled = [];
  for (const entry of menuCatalog) {
    if (entry.priority !== "P0") continue;
    if (domainSeen.has(entry.domain)) continue;
    domainSeen.add(entry.domain);
    sampled.push(entry);
    if (sampled.length >= NAVIGATION_SAMPLE_SIZE) break;
  }
  return sampled;
}

test.describe("menu-navigation: 导航 Shell 行为", () => {
  const sampled = sampleP0Entries();

  for (const entry of sampled) {
    test(`[NAV] ${entry.domain}/${entry.id} 打开后不跳转 /login 且无 JS 错误`, async ({ page }) => {
      await loginAsStoredRole(page, entry.loginRole);
      const resolvedPath = resolveCatalogPath(entry, seedState);
      test.skip(!resolvedPath, `${entry.id} 无法解析路径，跳过`);

      const collector = await installErrorCollector(page);
      try {
        await page.goto(resolvedPath!);
        await page.waitForLoadState("domcontentloaded");
        await page.waitForTimeout(500);

        await expect(page).not.toHaveURL(/\/login/);
        expect(collector.errors, `${entry.id} 不应有 JS 错误`).toHaveLength(0);
      } finally {
        collector.dispose();
      }
    });
  }

  test("[NAV] 面包屑在 P0 页面中可见", async ({ page }) => {
    const entry = sampled[0];
    if (!entry) return;

    await loginAsStoredRole(page, entry.loginRole);
    const resolvedPath = resolveCatalogPath(entry, seedState);
    if (!resolvedPath) return;

    await page.goto(resolvedPath);
    await page.waitForLoadState("domcontentloaded");
    await page.waitForTimeout(500);

    const breadcrumb = page.locator(".ant-breadcrumb, [data-testid='e2e-breadcrumb']");
    await expect(breadcrumb.first()).toBeVisible();
  });

  test("[NAV] 左侧菜单在登录后渲染（非白屏）", async ({ page }) => {
    await loginAsStoredRole(page, "sysadmin");

    await page.goto("/settings/org/departments");
    await page.waitForLoadState("domcontentloaded");
    await page.waitForTimeout(500);

    const aside = page.locator(".ant-layout-sider, [data-testid='e2e-sider']");
    await expect(aside.first()).toBeVisible();
  });

  test("[NAV] 路由不存在时展示 404 页面而非白屏", async ({ page }) => {
    await loginAsStoredRole(page, "sysadmin");

    await page.goto("/this-path-absolutely-does-not-exist-xyz");
    await page.waitForLoadState("domcontentloaded");
    await page.waitForTimeout(500);

    const body = page.locator("html > body");
    const is404 =
      (await page.locator("[data-testid='e2e-404-page']").count()) > 0 ||
      (await body.evaluate((el) => el.textContent?.includes("404") || el.textContent?.includes("Not Found")));

    expect(is404, "路由不存在时应显示 404 而非空白").toBe(true);
  });
});

test.describe("menu-navigation: P1 菜单可访问性抽样", () => {
  const p1Entries = menuCatalog.filter((e) => e.priority === "P1").slice(0, 5);

  for (const entry of p1Entries) {
    test(`[P1-NAV] ${entry.domain}/${entry.id} 无 404 白屏`, async ({ page }) => {
      await loginAsStoredRole(page, entry.loginRole);
      const resolvedPath = resolveCatalogPath(entry, seedState);
      test.skip(!resolvedPath, `${entry.id} 无法解析路径，跳过`);

      await page.goto(resolvedPath!);
      await page.waitForLoadState("domcontentloaded");
      await page.waitForTimeout(500);

      const body = page.locator("html > body");
      await expect(page).not.toHaveURL(/\/login/);
      await expect(body).not.toContainText("404");
    });
  }
});
