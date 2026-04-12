import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  captureEvidenceScreenshot,
  ensureAppSetup,
  uniqueName
} from "./helpers";

test.describe.serial("App Roles CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/roles`);
    await expect(page.getByTestId("app-roles-page")).toBeVisible();
  });

  test("create/update/delete role should work", async ({ page }, testInfo) => {
    const roleName = uniqueName("E2ERole");
    const roleCode = uniqueName("E2E_ROLE").replace(/-/g, "_").toUpperCase();
    const updatedRoleName = `${roleName}_edit`;

    await page.getByTestId("app-roles-create").click();
    await page.getByTestId("app-roles-form-name").fill(roleName);
    await page.getByTestId("app-roles-form-code").fill(roleCode);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await expect(page.getByTestId("app-roles-table")).toContainText(roleName);
    await captureEvidenceScreenshot(page, testInfo, "roles-created");

    const createdRow = page.locator(".ant-table-row", { hasText: roleName }).first();
    await expect(createdRow).toBeVisible();
    await createdRow.locator('[data-testid^="app-roles-edit-"]').first().click();
    await page.getByTestId("app-roles-form-name").fill(updatedRoleName);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await expect(page.getByTestId("app-roles-table")).toContainText(updatedRoleName);

    const editedRow = page.locator(".ant-table-row", { hasText: updatedRoleName }).first();
    await expect(editedRow).toBeVisible();
    await editedRow.locator('[data-testid^="app-roles-delete-"]').first().click();
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();

    await expect(page.getByTestId("app-roles-table")).not.toContainText(updatedRoleName);
    await captureEvidenceScreenshot(page, testInfo, "roles-deleted");
  });
});

