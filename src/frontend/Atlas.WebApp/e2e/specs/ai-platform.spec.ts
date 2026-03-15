import { test } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();
const entries = menuCatalog.filter((entry) => entry.domain === "ai");

for (const entry of entries) {
  test(`ai ${entry.id}`, async ({ page }) => {
    await loginAsStoredRole(page, entry.loginRole);
    test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
    await visitAndAssert(page, entry, seedState);
  });
}
