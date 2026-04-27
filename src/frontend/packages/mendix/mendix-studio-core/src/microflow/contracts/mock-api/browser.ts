import { setupWorker, type StartOptions, type SetupWorker } from "msw/browser";

import { createMicroflowContractMockHandlers } from "./index";

let worker: SetupWorker | undefined;

function isProduction(): boolean {
  const env = (import.meta as unknown as { env?: Record<string, string | boolean | undefined> }).env ?? {};
  return env.PROD === true || env.MODE === "production" || env.NODE_ENV === "production";
}

export async function startMicroflowMockWorker(options?: StartOptions): Promise<SetupWorker | undefined> {
  if (typeof window === "undefined" || isProduction()) {
    return undefined;
  }
  worker ??= setupWorker(...createMicroflowContractMockHandlers());
  await worker.start({
    onUnhandledRequest: "bypass",
    serviceWorker: { url: "/mockServiceWorker.js" },
    ...options,
  });
  return worker;
}

export async function startMicroflowContractMockWorker(options?: StartOptions): Promise<SetupWorker | undefined> {
  return startMicroflowMockWorker(options);
}

export function stopMicroflowMockWorker(): void {
  worker?.stop();
}
