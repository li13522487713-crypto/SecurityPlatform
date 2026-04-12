import { defineConfig, devices } from "@playwright/test";

const useManagedWebServers = process.env.PLAYWRIGHT_MANAGED_WEBSERVERS !== "0";
const appWebPort = 5181;
const appWebDevCommand = "pnpm run dev:app-web";

export default defineConfig({
  testDir: "./e2e/app",
  outputDir: "./test-results/app",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report/app", open: "never" }]
  ],
  expect: {
    timeout: 15_000
  },
  use: {
    baseURL: `http://127.0.0.1:${appWebPort}`,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 30_000
  },
  projects: [
    {
      name: "app-chromium",
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
