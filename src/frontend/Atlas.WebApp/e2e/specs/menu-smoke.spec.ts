import { test } from "@playwright/test";
import { menuCatalog, resolveCatalogPath } from "../catalog/menu-catalog";
import { loadSeedState, loginAsStoredRole, visitAndAssert } from "../helpers/test-helpers";

const seedState = loadSeedState();

test.describe("menu-smoke: P0 菜单全覆盖冒烟", () => {
  const p0Entries = menuCatalog.filter((e) => e.priority === "P0");

  for (const entry of p0Entries) {
    test(`smoke-P0 ${entry.domain}/${entry.id}`, async ({ page }) => {
      await loginAsStoredRole(page, entry.loginRole);
      test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
      await visitAndAssert(page, entry, seedState);
    });
  }
});

test.describe("menu-smoke: P1 菜单覆盖冒烟", () => {
  const p1Entries = menuCatalog.filter((e) => e.priority === "P1");

  for (const entry of p1Entries) {
    test(`smoke-P1 ${entry.domain}/${entry.id}`, async ({ page }) => {
      await loginAsStoredRole(page, entry.loginRole);
      test.skip(!resolveCatalogPath(entry, seedState), `${entry.id} has no resolved seed path`);
      await visitAndAssert(page, entry, seedState);
    });
  }
});

