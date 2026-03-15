import { test } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { ensureRestrictedRoute, loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();
const entries = menuCatalog.filter((entry) => entry.domain === "security");

for (const entry of entries) {
  test(`security ${entry.id}`, async ({ page }) => {
    await loginAsStoredRole(page, entry.loginRole);
    test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
    await visitAndAssert(page, entry, seedState);
  });
}

test("readonly user is redirected away from assets", async ({ page }) => {
  await loginAsStoredRole(page, "readonly");
  await ensureRestrictedRoute(page, "/assets");
});
