import { expect, test } from "@playwright/test";
import {
  clearAuthStorage,
  ensureAppSetup,
  loginApp,
  uniqueName
} from "./helpers";

test.describe.serial("App Departments And Positions CRUD", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test.beforeEach(async ({ page }) => {
    await clearAuthStorage(page);
    await loginApp(page, appKey);
  });

  test("create department", async ({ page }) => {
    const deptName = uniqueName("E2EDepartment");
    const deptCode = uniqueName("E2E_DEPT").replace(/-/g, "_").toUpperCase();

    await page.goto(`/apps/${encodeURIComponent(appKey)}/departments`);
    await expect(page.getByTestId("app-departments-page")).toBeVisible();
    await page.getByTestId("app-departments-create").click();
    await page.getByTestId("app-departments-form-name").fill(deptName);
    await page.getByTestId("app-departments-form-code").fill(deptCode);
    const createDeptResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/\d+\/organization\/departments$/.test(response.url())
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    const createDeptResponse = await createDeptResponsePromise;
    expect(createDeptResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-departments-page")).toBeVisible();
  });

  test("create position", async ({ page }) => {
    const positionName = uniqueName("E2EPosition");
    const positionCode = uniqueName("E2E_POS").replace(/-/g, "_").toUpperCase();

    await page.goto(`/apps/${encodeURIComponent(appKey)}/positions`);
    await expect(page.getByTestId("app-positions-page")).toBeVisible();
    await page.getByTestId("app-positions-create").click();
    await page.getByTestId("app-positions-form-name").fill(positionName);
    await page.getByTestId("app-positions-form-code").fill(positionCode);
    const createPositionResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        /\/api\/v2\/tenant-app-instances\/\d+\/organization\/positions$/.test(response.url())
    );
    await page.getByTestId("e2e-crud-drawer-submit").click();
    const createPositionResponse = await createPositionResponsePromise;
    expect(createPositionResponse.ok()).toBeTruthy();
    await expect(page.getByTestId("app-positions-page")).toBeVisible();
  });
});
