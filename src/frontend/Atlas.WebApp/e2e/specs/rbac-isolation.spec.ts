import { test, expect } from "@playwright/test";
import { roleMatrix } from "../catalog/role-matrix";
import { ensureRestrictedRoute, loginAsStoredRole } from "../helpers/test-helpers";

for (const rule of roleMatrix) {
  test(`rbac menu visibility ${rule.role}`, async ({ page }) => {
    await loginAsStoredRole(page, rule.role);

    for (const testId of rule.visibleMenuTestIds) {
      await expect(page.locator(`[data-testid="${testId}"]:visible`).first()).toBeVisible();
    }
  });

  for (const deniedPath of rule.deniedPaths) {
    test(`rbac deny ${rule.role} ${deniedPath}`, async ({ page }) => {
      await loginAsStoredRole(page, rule.role);
      await ensureRestrictedRoute(page, deniedPath);
    });
  }
}

test("project switcher reflects assigned projects when enabled", async ({ page }) => {
  await loginAsStoredRole(page, "userA");
  const switcher = page.getByTestId("e2e-project-switcher-select");
  test.skip(!(await switcher.count()), "project scope is not enabled for current app context");

  await switcher.click();
  await expect(page.locator(".ant-select-dropdown")).toContainText("E2E Project A");
});

test("readonly user cannot enter admin routes directly", async ({ page }) => {
  await loginAsStoredRole(page, "readonly");
  await ensureRestrictedRoute(page, "/settings/org/users");
  await ensureRestrictedRoute(page, "/settings/system/configs");
});
