import { test, expect } from "@playwright/test";
import { loadSeedState, loginAsStoredRole } from "../helpers/test-helpers";

const seedState = loadSeedState();

test.describe("console and workspace", () => {
  test("console quick cards navigate to expected pages", async ({ page }) => {
    await loginAsStoredRole(page, "superadmin");
    await page.goto("/console");

    await page.getByTestId("e2e-console-card-apps").click();
    await expect(page).toHaveURL(/\/console\/apps$/);

    await page.goto("/console");
    await page.getByTestId("e2e-console-card-datasources").click();
    await expect(page).toHaveURL(/\/console\/datasources$/);

    await page.goto("/console");
    await page.getByTestId("e2e-console-card-system-configs").click();
    await expect(page).toHaveURL(/\/console\/settings\/system\/configs$/);
  });

  test("workspace menu switches between dashboard builder runtime and settings", async ({ page }) => {
    test.skip(!seedState.lowCodeApps.e2e_workspace_app, "seeded workspace app is missing");

    await loginAsStoredRole(page, "appadmin");
    await page.goto(`/apps/${seedState.lowCodeApps.e2e_workspace_app}/dashboard`);
    await expect(page.getByTestId("e2e-app-workspace-layout")).toBeVisible();

    await page.getByTestId("e2e-app-workspace-menu-builder").click();
    await expect(page).toHaveURL(/\/builder$/);

    await page.getByTestId("e2e-app-workspace-menu-runtime").click();
    await expect(page).toHaveURL(/\/run\/home$/);

    await page.getByTestId("e2e-app-workspace-menu-settings").click();
    await expect(page).toHaveURL(/\/settings$/);
  });
});
