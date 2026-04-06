import { defineConfig, devices } from "@playwright/test";

const useManagedWebServers = process.env.PLAYWRIGHT_MANAGED_WEBSERVERS !== "0";

export default defineConfig({
  testDir: "./e2e/setup",
  outputDir: "./test-results/setup",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report/setup", open: "never" }]
  ],
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
      name: "setup-chromium",
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
          command: "cmd /c \"set PLAYWRIGHT_E2E=1&& pnpm run dev:app-web\"",
          url: "http://127.0.0.1:5181",
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        }
      ]
    : undefined
});
