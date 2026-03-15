import { test } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { assertCrudPage, loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();
const entries = menuCatalog.filter((entry) => entry.domain === "system");

for (const entry of entries) {
  test(`system ${entry.id}`, async ({ page }) => {
    await loginAsStoredRole(page, entry.loginRole);
    test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
    await visitAndAssert(page, entry, seedState);
  });
}

test("system users page exposes crud toolbar", async ({ page }) => {
  await loginAsStoredRole(page, "sysadmin");
  await page.goto("/settings/org/users");
  await assertCrudPage(page);
});
