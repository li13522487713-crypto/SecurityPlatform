import path from "node:path";
import { expect, test } from "@playwright/test";

test.describe.serial("真实浏览器 setup E2E", () => {
  const platformDatabasePath = "Data Source=atlas.e2e.db";
  const appDatabasePath = `Data Source=${path.resolve(process.cwd(), "../backend/Atlas.PlatformHost/atlas.e2e.db")}`;
  test.setTimeout(180_000);

  test.beforeEach(async ({ context }) => {
    await context.clearCookies();
    await context.addInitScript(() => {
      const preservedLocale = window.localStorage.getItem("atlas_locale");
      window.localStorage.clear();
      window.sessionStorage.clear();
      if (preservedLocale) {
        window.localStorage.setItem("atlas_locale", preservedLocale);
      }
    });
  });

  test("platform setup happy path", async ({ page, request }) => {
    const driversResponsePromise = page.waitForResponse((response) =>
      response.url().includes("/api/v1/setup/drivers") && response.request().method() === "GET"
    );

    await page.goto("http://127.0.0.1:5180/");
    await page.waitForURL("http://127.0.0.1:5180/setup");

    const driversResponse = await driversResponsePromise;
    expect(driversResponse.ok()).toBeTruthy();
    const driversPayload = await driversResponse.json();
    expect(Array.isArray(driversPayload?.data)).toBeTruthy();
    expect(driversPayload.data.some((driver: { code?: string }) => driver.code === "SQLite")).toBeTruthy();

    await page.getByTestId("platform-setup-start").click();

    const driverSelection = page.locator('[data-testid="platform-setup-driver"] .ant-select-selection-item');
    await expect(driverSelection).toContainText("SQLite", { timeout: 30_000 });

    const connectionStringInput = page.locator('input[data-testid="platform-setup-connection-string"], [data-testid="platform-setup-connection-string"] input');
    await connectionStringInput.fill(platformDatabasePath);
    await expect(connectionStringInput).toHaveValue(platformDatabasePath);

    await page.getByTestId("platform-setup-test-connection").click();
    await expect(page.getByTestId("platform-setup-test-result")).toBeVisible({ timeout: 30_000 });

    await page.getByTestId("platform-setup-next-step").click();

    await page.locator('input[data-testid="platform-setup-admin-password"], [data-testid="platform-setup-admin-password"] input').fill("P@ssw0rd!");
    await page.locator('input[data-testid="platform-setup-admin-password-confirm"], [data-testid="platform-setup-admin-password-confirm"] input').fill("P@ssw0rd!");

    await page.getByTestId("platform-setup-next-to-roles").click();
    await page.getByTestId("platform-setup-role-SecurityAdmin").click();
    await page.getByTestId("platform-setup-next-to-org").click();

    await expect(page.getByTestId("platform-setup-department-name-0")).toHaveValue("总部");
    await expect(page.getByTestId("platform-setup-position-code-0")).toHaveValue("SYS_ADMIN");
    await page.getByTestId("platform-setup-initialize").click();

    await expect(page.getByTestId("platform-setup-success")).toBeVisible({ timeout: 120_000 });
    await expect(page.getByTestId("platform-setup-report-status")).toHaveText("Ready");
    await expect(page.getByTestId("platform-setup-report-platform-completed")).toHaveText("true");
    await expect(page.getByTestId("platform-setup-report-schema")).toHaveText("true");
    await expect(page.getByTestId("platform-setup-report-seed")).toHaveText("true");
    const rolesCreatedText = await page.getByTestId("platform-setup-report-roles-created").innerText();
    const rolesCreated = Number.parseInt(rolesCreatedText, 10);
    expect(Number.isNaN(rolesCreated)).toBeFalsy();
    expect(rolesCreated).toBeGreaterThanOrEqual(0);
    await expect(page.getByTestId("platform-setup-report-departments-created")).toHaveText("3");
    await expect(page.getByTestId("platform-setup-report-positions-created")).toHaveText("2");
    await expect(page.getByTestId("platform-setup-report-admin")).toHaveText("true");
    await page.getByTestId("platform-setup-go-login").click();
    await page.waitForURL("http://127.0.0.1:5180/login");

    await expect.poll(async () => {
      const response = await request.get("http://127.0.0.1:5001/api/v1/setup/state");
      const payload = await response.json();
      return payload?.data?.status;
    }, { timeout: 30_000 }).toBe("Ready");
  });

  test("app setup happy path", async ({ page, request }) => {
    await page.goto("http://127.0.0.1:5181/");
    await page.waitForURL("http://127.0.0.1:5181/app-setup");

    await page.locator('input[data-testid="app-setup-connection-string"], [data-testid="app-setup-connection-string"] input').fill(appDatabasePath);
    await page.getByTestId("app-setup-test-connection").click();
    await expect(page.getByTestId("app-setup-test-result")).toBeVisible({ timeout: 30_000 });
    await page.getByTestId("app-setup-next-step").click();

    await page.locator('input[data-testid="app-setup-name"], [data-testid="app-setup-name"] input').fill("E2E Setup App");
    await page.locator('input[data-testid="app-setup-admin-username"], [data-testid="app-setup-admin-username"] input').fill("admin");
    await page.getByTestId("app-setup-initialize").click();

    await expect(page.getByTestId("app-setup-success")).toBeVisible({ timeout: 60_000 });
    await expect(page.getByTestId("app-setup-report-platform-status")).toHaveText("Ready");
    await expect(page.getByTestId("app-setup-report-app-status")).toHaveText("Ready");
    await expect(page.getByTestId("app-setup-report-app-completed")).toHaveText("true");
    await expect(page.getByTestId("app-setup-report-db-connected")).toHaveText("true");
    await expect(page.getByTestId("app-setup-report-core-tables")).toHaveText("true");
    await page.getByTestId("app-setup-enter-workspace").click();
    await page.waitForURL("http://127.0.0.1:5181/");

    await expect.poll(async () => {
      const response = await request.get("http://127.0.0.1:5002/api/v1/setup/state");
      const payload = await response.json();
      return {
        platformStatus: payload?.data?.platformStatus,
        appSetupCompleted: payload?.data?.appSetupCompleted
      };
    }, { timeout: 30_000 }).toEqual({
      platformStatus: "Ready",
      appSetupCompleted: true
    });
  });
});
