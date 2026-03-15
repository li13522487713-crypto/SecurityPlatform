import { test, expect } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();
const entries = menuCatalog.filter((entry) => entry.domain === "lowcode");

for (const entry of entries) {
  test(`lowcode ${entry.id}`, async ({ page }) => {
    await loginAsStoredRole(page, entry.loginRole);
    test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
    await visitAndAssert(page, entry, seedState);
  });
}

test("workspace runtime routes stay reachable", async ({ page }) => {
  test.skip(!seedState.lowCodeApps.e2e_workspace_app, "seeded workspace app is missing");

  await loginAsStoredRole(page, "appadmin");
  await page.goto(`/apps/${seedState.lowCodeApps.e2e_workspace_app}/dashboard`);
  await page.getByTestId("e2e-app-workspace-menu-dashboard").click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.getByTestId("e2e-app-workspace-menu-runtime").click();
  await expect(page).toHaveURL(/\/run\/home$/);
});
