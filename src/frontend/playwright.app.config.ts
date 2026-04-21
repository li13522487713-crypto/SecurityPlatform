import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

const useManagedWebServers = process.env.PLAYWRIGHT_MANAGED_WEBSERVERS !== "0";
const appWebPort = 5181;
const appWebDevCommand = "pnpm run dev:app-web";
const retainArtifacts = process.env.PLAYWRIGHT_DEBUG_ARTIFACTS === "1";
const e2eRunSeed = process.env.PLAYWRIGHT_E2E_RUN_ID ?? `${Date.now()}-${process.pid}`;
process.env.PLAYWRIGHT_E2E_RUN_ID = e2eRunSeed;
const platformSetupStatePath = path.resolve(__dirname, `../backend/Atlas.PlatformHost/setup-state.e2e.${e2eRunSeed}.json`);
const appSetupStatePath = path.resolve(__dirname, `../backend/Atlas.AppHost/app-setup-state.e2e.${e2eRunSeed}.json`);
const platformDbPath = path.resolve(__dirname, `../backend/Atlas.PlatformHost/atlas.e2e.platform.${e2eRunSeed}.db`);
const appDbPath = path.resolve(__dirname, `../backend/Atlas.AppHost/atlas.e2e.app.${e2eRunSeed}.db`);
const platformDbConnectionString = `Data Source=${platformDbPath}`;
const appDbConnectionString = `Data Source=${appDbPath}`;

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
    trace: retainArtifacts ? "retain-on-failure" : "off",
    screenshot: "only-on-failure",
    video: retainArtifacts ? "retain-on-failure" : "off",
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
          command: `cmd /c "set ASPNETCORE_ENVIRONMENT=Development&& set ASPNETCORE_URLS=http://127.0.0.1:5001&& set Setup__StateFilePath=${platformSetupStatePath}&& set Database__ConnectionString=${platformDbConnectionString}&& dotnet run --project ../backend/Atlas.PlatformHost --no-launch-profile"`,
          url: "http://127.0.0.1:5001/internal/health/live",
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          stdout: "pipe",
          stderr: "pipe"
        },
        {
          command: `cmd /c "set ASPNETCORE_ENVIRONMENT=Development&& set ASPNETCORE_URLS=http://127.0.0.1:5002&& set AppSetup__StateFilePath=${appSetupStatePath}&& set Database__ConnectionString=${appDbConnectionString}&& dotnet run --project ../backend/Atlas.AppHost --no-launch-profile"`,
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
