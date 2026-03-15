import { test, expect } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();
const entries = menuCatalog.filter((entry) => entry.domain === "approval");

for (const entry of entries) {
  test(`approval ${entry.id}`, async ({ page }) => {
    await loginAsStoredRole(page, entry.loginRole);
    test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
    await visitAndAssert(page, entry, seedState);
  });
}

test("approval workspace supports tab navigation", async ({ page }) => {
  await loginAsStoredRole(page, "approvaladmin");

  for (const tab of ["pending", "done", "requests", "cc"]) {
    await page.goto(`/approval/workspace?tab=${tab}`);
    await expect(page.getByTestId("e2e-shell-main")).toBeVisible();
  }
});
