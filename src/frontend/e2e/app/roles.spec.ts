import { expect, test } from "@playwright/test";
import {
  clearAuthStorage,
  ensureAppSetup,
  loginApp,
  uniqueName
} from "./helpers";

test.describe.serial("App Roles CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
    await loginApp(page, appKey);
    await page.goto(`/apps/${encodeURIComponent(appKey)}/roles`);
    await expect(page.getByTestId("app-roles-page")).toBeVisible();
  });

  test("create role and update role name", async ({ page }) => {
    const roleName = uniqueName("E2ERole");
    const roleCode = uniqueName("E2E_ROLE").replace(/-/g, "_").toUpperCase();
    const updatedRoleName = `${roleName}_edit`;

    await page.getByTestId("app-roles-create").click();
    await page.getByTestId("app-roles-form-name").fill(roleName);
    await page.getByTestId("app-roles-form-code").fill(roleCode);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await expect(page.getByTestId("app-roles-table")).toContainText(roleName);

    const row = page.locator(".ant-table-row", { hasText: roleCode }).first();
    await expect(row).toBeVisible();
    await row.locator('[data-testid^="app-roles-edit-"]').first().click();
    await page.getByTestId("app-roles-form-name").fill(updatedRoleName);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await expect(page.getByTestId("app-roles-table")).toContainText(updatedRoleName);
  });
});
