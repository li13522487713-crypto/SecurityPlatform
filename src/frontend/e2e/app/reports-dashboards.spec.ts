import { expect, test } from "@playwright/test";
import {
  clearAuthStorage,
  ensureAppSetup,
  loginApp,
  uniqueName
} from "./helpers";

test.describe.serial("App Reports And Dashboards CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
    await loginApp(page, appKey);
  });

  test("create report and dashboard", async ({ page }) => {
    const reportName = uniqueName("E2EReport");
    const dashboardName = uniqueName("E2EDashboard");

    await page.goto(`/apps/${encodeURIComponent(appKey)}/reports`);
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

    await page.goto(`/apps/${encodeURIComponent(appKey)}/dashboards`);
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
  });
});
