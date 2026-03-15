import type { FullConfig } from "@playwright/test";
import { request } from "@playwright/test";
import { ensureE2eStateDirs } from "./helpers/auth-state";
import { seedE2EState } from "./helpers/seed";

const defaultApiBaseUrl = process.env.E2E_API_BASE_URL ?? "http://127.0.0.1:5000/api/v1";

export default async function globalSetup(config: FullConfig) {
  ensureE2eStateDirs();

  const requestContext = await request.newContext();
  try {
    const licenseResponse = await requestContext.get(`${defaultApiBaseUrl}/license/status`);
    if (!licenseResponse.ok()) {
      throw new Error(`License status check failed with HTTP ${licenseResponse.status()}`);
    }

    const payload = await licenseResponse.json();
    const status = payload?.data?.status ?? payload?.status;
    if (status !== "Active") {
      throw new Error(`License must be Active before E2E runs. Current status: ${String(status)}`);
    }
  } finally {
    await requestContext.dispose();
  }

  const baseURL = config.projects[0]?.use?.baseURL;
  if (!baseURL) {
    throw new Error("Playwright baseURL is not configured");
  }

  await seedE2EState(defaultApiBaseUrl);
}
