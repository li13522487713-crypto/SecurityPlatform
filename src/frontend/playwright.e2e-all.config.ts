import { defineConfig, devices } from "@playwright/test";

const useManagedWebServers = process.env.PLAYWRIGHT_MANAGED_WEBSERVERS !== "0";
const appWebMode = process.env.PLAYWRIGHT_APP_WEB_MODE === "direct" ? "direct" : "platform";
const appWebPort = appWebMode === "direct" ? 5182 : 5181;
const appWebDevCommand = appWebMode === "direct" ? "pnpm run dev:app-web:direct" : "pnpm run dev:app-web";

export default defineConfig({
  testDir: "./e2e",
  testMatch: /e2e-all\.ordered\.spec\.ts$/,
  outputDir: "./test-results/all",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report/all", open: "never" }]
  ],
  expect: {
    timeout: 15_000
  },
  use: {
    baseURL: "http://127.0.0.1:5180",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 30_000
  },
  projects: [
    {
      name: "all-chromium",
      use: {
        ...devices["Desktop Chrome"],
        channel: undefined,
        browserName: "chromium"
      }
    }
  ],
  webServer: useManagedWebServers
    ? [
        {
          command: "cmd /c \"set ASPNETCORE_ENVIRONMENT=Development&& set ASPNETCORE_URLS=http://127.0.0.1:5001&& dotnet run --project ../backend/Atlas.PlatformHost --no-launch-profile\"",
          url: "http://127.0.0.1:5001/internal/health/live",
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        },
        {
          command: "cmd /c \"set ASPNETCORE_ENVIRONMENT=Development&& set ASPNETCORE_URLS=http://127.0.0.1:5002&& dotnet run --project ../backend/Atlas.AppHost --no-launch-profile\"",
          url: "http://127.0.0.1:5002/internal/health/live",
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        },
        {
          command: "cmd /c \"set PLAYWRIGHT_E2E=1&& pnpm run dev:platform-web\"",
          url: "http://127.0.0.1:5180",
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        },
        {
          command: `cmd /c "set PLAYWRIGHT_E2E=1&& ${appWebDevCommand}"`,
          url: `http://127.0.0.1:${appWebPort}`,
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        }
      ]
    : undefined
});
