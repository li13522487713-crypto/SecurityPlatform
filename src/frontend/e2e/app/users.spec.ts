import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  captureEvidenceScreenshot,
  ensureAppSetup,
  uniqueName
} from "./helpers";

test.describe.serial("App Users CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/users`);
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
    const createUserResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/organization\/members\/users$/.test(response.url()) &&
        response.status() < 400
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await createUserResponsePromise;

    await expect(page.getByTestId("app-users-table")).toContainText(displayName);
    await captureEvidenceScreenshot(page, testInfo, "users-created");

    const row = page.locator(".ant-table-row", { hasText: username }).first();
    await expect(row).toBeVisible();
    await row.locator('[data-testid^="app-users-edit-"]').first().click();
    await page.getByTestId("app-users-edit-display-name").fill(editedDisplayName);
    const updateProfilePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/organization\/members\/[^/]+\/profile$/.test(response.url()) &&
        response.status() < 400
    );
    const updateRolesPromise = page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/organization\/members\/[^/]+\/roles$/.test(response.url()) &&
        response.status() < 400
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await Promise.all([updateProfilePromise, updateRolesPromise]);

    await expect(page.getByTestId("app-users-table")).toContainText(editedDisplayName);

    const editedRow = page.locator(".ant-table-row", { hasText: username }).first();
    await editedRow.locator('[data-testid^="app-users-reset-password-"]').first().click();
    await page.getByTestId("app-users-reset-password-input").fill("P@ssw0rd!456");
    const resetPasswordPromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/organization\/members\/[^/]+\/reset-password$/.test(response.url()) &&
        response.status() < 400
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await resetPasswordPromise;

    const latestRow = page.locator(".ant-table-row", { hasText: username }).first();
    const deleteUserPromise = page.waitForResponse(
      (response) =>
        response.request().method() === "DELETE" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/organization\/members\/[^/]+$/.test(response.url()) &&
        response.status() < 400
    );
    const removeButton = latestRow.locator('[data-testid^="app-users-remove-"]').first();
    await removeButton.click();
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();
    await deleteUserPromise;

    await expect(page.getByTestId("app-users-table")).not.toContainText(username);
    await captureEvidenceScreenshot(page, testInfo, "users-deleted");
  });
});

