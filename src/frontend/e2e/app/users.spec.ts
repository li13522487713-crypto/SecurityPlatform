import { expect, test } from "@playwright/test";
import {
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

  test("create user and update display name", async ({ page }) => {
    const username = uniqueName("e2e_user");
    const displayName = uniqueName("E2EUser");
    const editedDisplayName = `${displayName}_edit`;

    await page.getByTestId("app-users-create").click();
    await page.getByTestId("app-users-form-username").fill(username);
    await page.getByTestId("app-users-form-password").fill("P@ssw0rd!123");
    await page.getByTestId("app-users-form-display-name").fill(displayName);
    await page.getByTestId("e2e-crud-drawer-submit").click();

    await expect(page.getByTestId("app-users-table")).toContainText(displayName);

    const row = page.locator(".ant-table-row", { hasText: username }).first();
    await expect(row).toBeVisible();
    await row.locator('[data-testid^="app-users-edit-"]').first().click();
    await page.getByTestId("app-users-edit-display-name").fill(editedDisplayName);
    await page.getByTestId("e2e-crud-drawer-submit").click();

    await expect(page.getByTestId("app-users-table")).toContainText(editedDisplayName);
  });
});
