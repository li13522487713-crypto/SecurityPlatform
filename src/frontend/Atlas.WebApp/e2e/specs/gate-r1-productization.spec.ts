import { expect, test } from "@playwright/test";
import path from "node:path";
import fs from "node:fs";
import { fileURLToPath } from "node:url";

const currentDir = path.dirname(fileURLToPath(import.meta.url));
const evidenceDir = path.resolve(currentDir, "../../../../../docs/evidence/gate-r1-20260308");

function ensureEvidenceDir() {
  if (!fs.existsSync(evidenceDir)) {
    fs.mkdirSync(evidenceDir, { recursive: true });
  }
}

async function login(page: Parameters<typeof test>[0]["page"]) {
  const tenantId = "00000000-0000-0000-0000-000000000001";
  const loginResp = await page.request.post("/api/v1/auth/token", {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    data: {
      username: "admin",
      password: "P@ssw0rd!"
    }
  });
  expect(loginResp.ok()).toBeTruthy();
  const loginJson = await loginResp.json() as { data: { accessToken: string; refreshToken: string } };

  await page.goto("/login");
  await page.evaluate(({ accessToken, refreshToken, tenantId: tid }) => {
    sessionStorage.setItem("access_token", accessToken);
    localStorage.setItem("refresh_token", refreshToken);
    localStorage.setItem("tenant_id", tid);
  }, {
    accessToken: loginJson.data.accessToken,
    refreshToken: loginJson.data.refreshToken,
    tenantId
  });

  await page.goto("/console");
  await page.waitForURL(/\/console/, { timeout: 20_000 });
  return loginJson.data.accessToken;
}

test("Gate-R1: 平台-应用-运行-治理关键路径截图取证", async ({ page }) => {
  test.setTimeout(120_000);
  ensureEvidenceDir();

  await login(page);

  await expect(page).toHaveURL(/\/console/);
  await page.screenshot({ path: path.join(evidenceDir, "01-console-home.png"), fullPage: true });

  await page.goto("/console/apps");
  await expect(page.getByText("应用列表")).toBeVisible();
  await page.screenshot({ path: path.join(evidenceDir, "02-console-apps.png"), fullPage: true });

  await page.goto("/console/tools");
  await expect(page.getByText("Tools Authorization Center")).toBeVisible();
  await page.screenshot({ path: path.join(evidenceDir, "03-console-tools.png"), fullPage: true });

  await page.goto("/settings/license");
  await expect(page.locator(".ant-page-header-heading-title").filter({ hasText: "授权管理" })).toBeVisible();
  await page.screenshot({ path: path.join(evidenceDir, "04-license-center.png"), fullPage: true });

  await page.goto("/console/apps");
  await page.getByRole("button", { name: "新建应用" }).click();
  await expect(page.locator(".ant-modal")).toBeVisible({ timeout: 10_000 });
  await page.screenshot({ path: path.join(evidenceDir, "05-app-create-wizard.png"), fullPage: true });

  await page.goto("/apps/1/dashboard");
  await page.waitForLoadState("networkidle");
  await page.screenshot({ path: path.join(evidenceDir, "07-app-dashboard.png"), fullPage: true });
  await page.goto("/apps/1/settings");
  await expect(page.locator(".ant-page-header-heading-title").filter({ hasText: "应用设置" })).toBeVisible();
  await page.screenshot({ path: path.join(evidenceDir, "08-app-settings.png"), fullPage: true });

  await page.goto("/r/crm_demo_2026/customer-form");
  await page.waitForLoadState("networkidle");
  await page.screenshot({ path: path.join(evidenceDir, "09-runtime-route.png"), fullPage: true });
});
