import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  captureEvidenceScreenshot,
  ensureAppSetup,
  uniqueName
} from "./helpers";

test.describe.serial("App Reports And Dashboards CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("report and dashboard full crud should work", async ({ page }, testInfo) => {
    const reportName = uniqueName("E2EReport");
    const editedReportName = `${reportName}_edit`;
    const dashboardName = uniqueName("E2EDashboard");
    const editedDashboardName = `${dashboardName}_edit`;

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/reports`);
    await expect(page.getByTestId("app-reports-page")).toBeVisible();
    await page.getByTestId("app-reports-create").click();
    await page.getByTestId("app-reports-form-name").fill(reportName);
    const createReportResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v1\/reports$/.test(response.url())
    );
    await page.locator(".ant-modal .ant-btn-primary").last().click();
    const createReportResponse = await createReportResponsePromise;
    expect(createReportResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-reports-table")).toContainText(reportName);
    await captureEvidenceScreenshot(page, testInfo, "reports-created");

    const reportRow = page.locator(".ant-table-row", { hasText: reportName }).first();
    await expect(reportRow).toBeVisible();
    await reportRow.locator('[data-testid^="app-reports-edit-"]').first().click();
    await page.getByTestId("app-reports-form-name").fill(editedReportName);
    const updateReportResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/api\/v1\/reports\/[^/]+$/.test(response.url())
    );
    await page.locator(".ant-modal .ant-btn-primary").last().click();
    const updateReportResponse = await updateReportResponsePromise;
    expect(updateReportResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-reports-table")).toContainText(editedReportName);

    const editedReportRow = page.locator(".ant-table-row", { hasText: editedReportName }).first();
    await expect(editedReportRow).toBeVisible();
    await editedReportRow.locator('[data-testid^="app-reports-delete-"]').first().click();
    const deleteReportResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "DELETE" &&
        /\/api\/v1\/reports\/[^/]+$/.test(response.url())
    );
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();
    const deleteReportResponse = await deleteReportResponsePromise;
    expect(deleteReportResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-reports-table")).not.toContainText(reportName);
    await captureEvidenceScreenshot(page, testInfo, "reports-deleted");

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboards`);
    await expect(page.getByTestId("app-dashboards-page")).toBeVisible();
    await page.getByTestId("app-dashboards-create").click();
    await page.getByTestId("app-dashboards-form-name").fill(dashboardName);
    const createDashboardResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v1\/dashboards$/.test(response.url())
    );
    await page.locator(".ant-modal .ant-btn-primary").last().click();
    const createDashboardResponse = await createDashboardResponsePromise;
    expect(createDashboardResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-dashboards-table")).toContainText(dashboardName);
    await captureEvidenceScreenshot(page, testInfo, "dashboards-created");

    const dashboardRow = page.locator(".ant-table-row", { hasText: dashboardName }).first();
    await expect(dashboardRow).toBeVisible();
    await dashboardRow.locator('[data-testid^="app-dashboards-edit-"]').first().click();
    await page.getByTestId("app-dashboards-form-name").fill(editedDashboardName);
    const updateDashboardResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/api\/v1\/dashboards\/[^/]+$/.test(response.url())
    );
    await page.locator(".ant-modal .ant-btn-primary").last().click();
    const updateDashboardResponse = await updateDashboardResponsePromise;
    expect(updateDashboardResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-dashboards-table")).toContainText(editedDashboardName);

    const editedDashboardRow = page.locator(".ant-table-row", { hasText: editedDashboardName }).first();
    await expect(editedDashboardRow).toBeVisible();
    await editedDashboardRow.locator('[data-testid^="app-dashboards-delete-"]').first().click();
    const deleteDashboardResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "DELETE" &&
        /\/api\/v1\/dashboards\/[^/]+$/.test(response.url())
    );
    await page.locator(".ant-popconfirm-buttons .ant-btn-primary").last().click();
    const deleteDashboardResponse = await deleteDashboardResponsePromise;
    expect(deleteDashboardResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-dashboards-table")).not.toContainText(dashboardName);
    await captureEvidenceScreenshot(page, testInfo, "dashboards-deleted");
  });
});

