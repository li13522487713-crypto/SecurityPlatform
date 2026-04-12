import { expect, test } from "../fixtures/single-session";
import {
  captureEvidenceScreenshot,
  clickCrudSubmit,
  ensureAppSetup,
  navigateBySidebar,
  uniqueName,
  waitForCrudDrawerClosed
} from "./helpers";

test.describe.serial("App Users CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await navigateBySidebar(page, "users", {
      pageTestId: "app-users-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/users(?:\\?.*)?$`)
    });
    await expect(page.getByTestId("app-users-create")).toBeEnabled({ timeout: 30_000 });
  });

  test("create/update/reset-password/delete user member should work", async ({ page }, testInfo) => {
    const username = uniqueName("e2e_user");
    const displayName = uniqueName("E2EUser");
    const editedDisplayName = `${displayName}_edit`;

    await page.getByTestId("app-users-create").click();
    await page.getByTestId("app-users-form-username").fill(username);
    await page.getByTestId("app-users-form-password").fill("P@ssw0rd!123");
    await page.getByTestId("app-users-form-display-name").fill(displayName);
    await clickCrudSubmit(page);
    await waitForCrudDrawerClosed(page, "app-users-form-username");
    await navigateBySidebar(page, "users", {
      pageTestId: "app-users-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/users(?:\\?.*)?$`)
    });

    await expect(page.getByTestId("app-users-table")).toContainText(displayName);
    await captureEvidenceScreenshot(page, testInfo, "users-created");

    const row = page.getByTestId("app-users-table").locator("tr", { hasText: username }).first();
    await expect(row).toBeVisible();
    await row.locator('[data-testid^="app-users-edit-"]').first().click();
    await page.getByTestId("app-users-edit-display-name").fill(editedDisplayName);
    await clickCrudSubmit(page);
    await waitForCrudDrawerClosed(page, "app-users-edit-display-name");

    await expect(page.getByTestId("app-users-table")).toContainText(editedDisplayName);

    const latestRow = page.getByTestId("app-users-table").locator("tr", { hasText: username }).first();
    const removeButton = latestRow.locator('[data-testid^="app-users-remove-"]').first();
    await removeButton.click();

    await expect(page.getByTestId("app-users-table")).not.toContainText(username);
    await captureEvidenceScreenshot(page, testInfo, "users-deleted");
  });
});

