import { test, expect } from "@playwright/test";
import { assertTableView, captureWriteRequest, loginAsStoredRole } from "../helpers/test-helpers";

test.describe("security headers and table view", () => {
  test("users page saves a table view with csrf and idempotency headers", async ({ page }) => {
    await loginAsStoredRole(page, "sysadmin");
    await page.goto("/settings/org/users");
    await expect(page.getByTestId("e2e-table-view-toolbar")).toBeVisible();

    const viewName = `e2e-users-${Date.now()}`;
    await captureWriteRequest(page, "/table-views", async () => {
      await assertTableView(page, viewName);
    });
  });
});
