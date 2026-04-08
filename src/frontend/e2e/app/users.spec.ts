import { expect, test } from "@playwright/test";
import {
  captureEvidenceScreenshot,
  clearAuthStorage,
  ensureAppSetup,
  loginApp,
  uniqueName
} from "./helpers";

test.describe.serial("App Users CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
    await loginApp(page, appKey);
    await page.goto(`/apps/${encodeURIComponent(appKey)}/users`);
    await expect(page.getByTestId("app-users-page")).toBeVisible();
  });

  test("create/update/reset-password/delete user member should work", async ({ page }, testInfo) => {
    const username = uniqueName("e2e_user");
    const displayName = uniqueName("E2EUser");
    const editedDisplayName = `${displayName}_edit`;

    await page.getByTestId("app-users-create").click();
    await page.getByTestId("app-users-form-username").fill(username);
    await page.getByTestId("app-users-form-password").fill("P@ssw0rd!123");
    await page.getByTestId("app-users-form-display-name").fill(displayName);
    await page.getByTestId("e2e-crud-drawer-submit").click();

    await expect(page.getByTestId("app-users-table")).toContainText(displayName);
    await captureEvidenceScreenshot(page, testInfo, "users-created");

    const row = page.locator(".ant-table-row", { hasText: username }).first();
    await expect(row).toBeVisible();
    await row.locator('[data-testid^="app-users-edit-"]').first().click();
    await page.getByTestId("app-users-edit-display-name").fill(editedDisplayName);
    await page.getByTestId("e2e-crud-drawer-submit").click();

    await expect(page.getByTestId("app-users-table")).toContainText(editedDisplayName);

    await row.locator('[data-testid^="app-users-reset-password-"]').first().click();
    await page.getByTestId("app-users-reset-password-input").fill("P@ssw0rd!456");
    await page.getByTestId("e2e-crud-drawer-submit").click();

    const removeButton = row.locator('[data-testid^="app-users-remove-"]').first();
    await removeButton.click();
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();

    await expect(page.getByTestId("app-users-table")).not.toContainText(username);
    await captureEvidenceScreenshot(page, testInfo, "users-deleted");
  });
});
