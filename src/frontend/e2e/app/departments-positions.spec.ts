import { expect, test } from "../fixtures/single-session";
import {
  captureEvidenceScreenshot,
  ensureAppSetup,
  navigateBySidebar,
  uniqueName,
  waitForCrudDrawerClosed
} from "./helpers";

test.describe.serial("App Departments And Positions CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("department create/delete should work", async ({ page }, testInfo) => {
    const deptName = uniqueName("E2EDepartment");
    const deptCode = uniqueName("E2E_DEPT").replace(/-/g, "_").toUpperCase();

    await navigateBySidebar(page, "departments", {
      pageTestId: "app-departments-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/departments(?:\\?.*)?$`)
    });
    await page.getByTestId("app-departments-create").click();
    await page.getByTestId("app-departments-form-name").fill(deptName);
    await page.getByTestId("app-departments-form-code").fill(deptCode);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await waitForCrudDrawerClosed(page, "app-departments-form-code");

    let departmentRowVisible = false;
    for (let attempt = 0; attempt < 12; attempt += 1) {
      const row = page.locator(".ant-table-row", { hasText: deptName }).first();
      if ((await row.count()) > 0 && await row.isVisible()) {
        departmentRowVisible = true;
        break;
      }

      const collapsedExpandIcon = page.locator(".ant-table-row-expand-icon-collapsed").first();
      if ((await collapsedExpandIcon.count()) > 0) {
        await collapsedExpandIcon.click();
      } else {
        await page.getByTestId("app-departments-toggle-expand").click();
      }

      await page.waitForTimeout(500);
    }

    expect(departmentRowVisible, "创建成功但部门行未在树表中出现").toBeTruthy();
    await captureEvidenceScreenshot(page, testInfo, "departments-created");

    const createdDeptRow = page.locator(".ant-table-row", { hasText: deptName }).first();
    await expect(createdDeptRow).toBeVisible();
    await createdDeptRow.locator('[data-testid^="app-departments-delete-"]').first().click();
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();
    await expect(page.getByTestId("app-departments-table")).not.toContainText(deptName);
    await captureEvidenceScreenshot(page, testInfo, "departments-deleted");
  });

  test("position create/delete should work", async ({ page }, testInfo) => {
    const positionName = uniqueName("E2EPosition");
    const positionCode = uniqueName("E2E_POS").replace(/-/g, "_").toUpperCase();

    await navigateBySidebar(page, "positions", {
      pageTestId: "app-positions-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/positions(?:\\?.*)?$`)
    });
    await page.getByTestId("app-positions-create").click();
    await page.getByTestId("app-positions-form-name").fill(positionName);
    await page.getByTestId("app-positions-form-code").fill(positionCode);
    await page.getByTestId("e2e-crud-drawer-submit").click();
    await waitForCrudDrawerClosed(page, "app-positions-form-code");
    await expect(page.getByTestId("app-positions-table")).toContainText(positionName);
    await captureEvidenceScreenshot(page, testInfo, "positions-created");

    const positionRow = page.locator(".ant-table-row", { hasText: positionName }).first();
    await expect(positionRow).toBeVisible();
    await positionRow.locator('[data-testid^="app-positions-delete-"]').first().click();
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();
    await expect(page.getByTestId("app-positions-table")).not.toContainText(positionName);
    await captureEvidenceScreenshot(page, testInfo, "positions-deleted");
  });
});

