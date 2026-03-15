import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.PLAYWRIGHT_PORT ?? 5173);
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./e2e/specs",
  testIgnore: [
    "**/gui-tests.spec.ts",
    "**/auth-security.spec.ts",
    "**/assets.spec.ts",
    "**/dynamic-data.spec.ts",
    "**/gate-r1-productization.spec.ts",
    "**/identity-rbac.spec.ts",
    "**/lowcode.spec.ts",
    "**/monitoring.spec.ts",
    "**/smoke.spec.ts",
    "**/system-ops.spec.ts",
    "**/visualization.spec.ts",
    "**/rbac/**/*.spec.ts"
  ],
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: "list",
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure"
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] }
    }
  ],
  webServer: {
    command: `npm run dev -- --host 127.0.0.1 --port ${port}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120 * 1000
  }
});
