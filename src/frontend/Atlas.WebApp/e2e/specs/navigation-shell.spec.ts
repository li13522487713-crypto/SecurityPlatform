import { test, expect } from "@playwright/test";
import { loginAsStoredRole } from "../helpers/test-helpers";

test.describe("navigation shell", () => {
  test("renders shell landmarks and notification panel", async ({ page }) => {
    await loginAsStoredRole(page, "superadmin");

    await expect(page.getByTestId("e2e-shell-main")).toBeVisible();
    await expect(page.getByTestId("e2e-sidebar")).toBeVisible();
    await expect(page.getByTestId("e2e-header")).toBeVisible();
    await expect(page.getByTestId("e2e-tags-view")).toBeVisible();
    await expect(page.getByTestId("e2e-breadcrumb")).toBeVisible();

    const projectSwitcher = page.getByTestId("e2e-project-switcher");
    if (await projectSwitcher.count()) {
      await expect(projectSwitcher).toBeVisible();
    }

    await page.getByTestId("e2e-notification-bell").click();
    await expect(page.getByTestId("e2e-notification-panel")).toBeVisible();
  });

  test("opens profile and logs out", async ({ page }) => {
    await loginAsStoredRole(page, "superadmin");

    await page.getByTestId("e2e-user-menu-trigger").click();
    await page.getByTestId("e2e-user-menu-profile").click();
    await expect(page).toHaveURL(/\/profile$/);

    await page.getByTestId("e2e-user-menu-trigger").click();
    await page.getByTestId("e2e-user-menu-logout").click();
    await expect(page).toHaveURL(/\/login/);
  });
});
