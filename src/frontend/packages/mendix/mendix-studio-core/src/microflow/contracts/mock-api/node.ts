import { setupServer, type SetupServerApi } from "msw/node";

import { createMicroflowContractMockHandlers } from "./index";
import { resetMicroflowContractMockStore } from "./mock-api-store";

let server: SetupServerApi | undefined;

export function startMicroflowMockServer(): SetupServerApi {
  server ??= setupServer(...createMicroflowContractMockHandlers());
  server.listen({ onUnhandledRequest: "bypass" });
  return server;
}

export function resetMicroflowMockServer(): void {
  resetMicroflowContractMockStore();
  server?.resetHandlers(...createMicroflowContractMockHandlers());
}

export function stopMicroflowMockServer(): void {
  server?.close();
  server = undefined;
}
