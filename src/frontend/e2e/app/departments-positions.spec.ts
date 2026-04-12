import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  captureEvidenceScreenshot,
  ensureAppSetup,
  uniqueName
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

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/departments`);
    await expect(page.getByTestId("app-departments-page")).toBeVisible();
    await page.getByTestId("app-departments-create").click();
    await page.getByTestId("app-departments-form-name").fill(deptName);
    await page.getByTestId("app-departments-form-code").fill(deptCode);
    const createDeptResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/departments$/.test(response.url()) &&
        response.status() < 400
    );
    const listAfterCreateResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "GET" &&
        /\/api\/v2\/tenant-app-instances\/\d+\/departments\/all(?:\?.*)?$/.test(response.url()) &&
        response.status() < 400
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    const createDeptResponse = await createDeptResponsePromise;
    const listAfterCreateResponse = await listAfterCreateResponsePromise;
    expect(createDeptResponse.ok()).toBeTruthy();
    const createDeptPayload = (await createDeptResponse.json()) as { success?: boolean; message?: string };
    expect(createDeptPayload.success, createDeptPayload.message ?? "创建部门接口返回 success=false").toBeTruthy();
    expect(listAfterCreateResponse.ok()).toBeTruthy();
    const listAfterCreatePayload = (await listAfterCreateResponse.json()) as {
      success?: boolean;
      message?: string;
      data?: Array<{ name?: string }>;
    };
    expect(listAfterCreatePayload.success, listAfterCreatePayload.message ?? "部门列表接口返回 success=false").toBeTruthy();
    expect(
      listAfterCreatePayload.data?.some((item) => item.name === deptName),
      "创建后部门列表未返回新部门"
    ).toBeTruthy();

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

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/positions`);
    await expect(page.getByTestId("app-positions-page")).toBeVisible();
    await page.getByTestId("app-positions-create").click();
    await page.getByTestId("app-positions-form-name").fill(positionName);
    await page.getByTestId("app-positions-form-code").fill(positionCode);
    const createPositionResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/[^/]+\/positions$/.test(response.url()) &&
        response.status() < 400
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    const createPositionResponse = await createPositionResponsePromise;
    expect(createPositionResponse.ok()).toBeTruthy();
    const createPositionPayload = (await createPositionResponse.json()) as { success?: boolean; message?: string };
    expect(createPositionPayload.success, createPositionPayload.message ?? "创建职位接口返回 success=false").toBeTruthy();
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

