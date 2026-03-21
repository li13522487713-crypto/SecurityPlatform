import { test, expect } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { roleMatrix } from "../catalog/role-matrix";
import { loadSeedState, loginAsStoredRole, ensureRestrictedRoute } from "../helpers/test-helpers";

const seedState = loadSeedState();

test.describe("menu-rbac: P0 菜单 - 有权访问角色可正常打开", () => {
  const p0Entries = menuCatalog.filter((e) => e.priority === "P0");

  for (const entry of p0Entries) {
    test(`[P0] ${entry.domain}/${entry.id} 主角色可访问无白屏`, async ({ page }) => {
      await loginAsStoredRole(page, entry.loginRole);
      const resolvedPath = resolveCatalogPath(entry, seedState);
      test.skip(!resolvedPath, `${entry.id} 无法解析路径，跳过`);

      await page.goto(resolvedPath!);
      await page.waitForLoadState("domcontentloaded");
      await page.waitForTimeout(500);

      const body = page.locator("html > body");
      await expect(page).not.toHaveURL(/\/login/);
      await expect(body).not.toContainText("404");
      await expect(body).not.toContainText("Not Found");
    });
  }
});

test.describe("menu-rbac: P0 菜单 - restrictedRole 拒绝访问", () => {
  const restrictedEntries = menuCatalog.filter(
    (e) => e.priority === "P0" && e.restrictedRole
  );

  for (const entry of restrictedEntries) {
    test(`[P0-RBAC] ${entry.domain}/${entry.id} 受限角色无法访问`, async ({ page }) => {
      await loginAsStoredRole(page, entry.restrictedRole!);
      const resolvedPath = resolveCatalogPath(entry, seedState);
      test.skip(!resolvedPath, `${entry.id} 无法解析路径，跳过`);

      await ensureRestrictedRoute(page, resolvedPath!);
    });
  }
});

test.describe("menu-rbac: 角色矩阵 - deniedPaths 均被拦截", () => {
  for (const rule of roleMatrix) {
    for (const deniedPath of rule.deniedPaths) {
      test(`[矩阵] ${rule.role} 无权访问 ${deniedPath}`, async ({ page }) => {
        await loginAsStoredRole(page, rule.role);
        await ensureRestrictedRoute(page, deniedPath);
      });
    }
  }
});

test.describe("menu-rbac: 未登录用户重定向到 login", () => {
  const p0Paths = menuCatalog
    .filter((e) => e.priority === "P0")
    .map((e) => resolveCatalogPath(e, seedState))
    .filter(Boolean)
    .slice(0, 3);

  for (const path of p0Paths) {
    test(`未认证访问 ${path} 跳转登录`, async ({ page }) => {
      await page.context().clearCookies();
      await page.goto(path!);
      await page.waitForLoadState("domcontentloaded");
      await page.waitForTimeout(500);
      await expect(page).toHaveURL(/\/login/);
    });
  }
});
