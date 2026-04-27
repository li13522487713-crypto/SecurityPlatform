import { parseMicroflowAdapterMode, type MicroflowAdapterFactoryConfig } from "@atlas/mendix-studio-core";

function isMicroflowContractMockEnabled(): boolean {
  return (import.meta.env.VITE_MICROFLOW_API_MOCK ?? import.meta.env.MICROFLOW_API_MOCK) === "msw";
}

export function createAppMicroflowAdapterConfig(input: {
  workspaceId?: string;
  tenantId?: string;
  currentUser?: MicroflowAdapterFactoryConfig["currentUser"];
}): MicroflowAdapterFactoryConfig {
  const mode = parseMicroflowAdapterMode(import.meta.env.VITE_MICROFLOW_ADAPTER_MODE ?? import.meta.env.MICROFLOW_ADAPTER_MODE);
  const contractMockEnabled = isMicroflowContractMockEnabled();
  const microflowApiBaseUrl = import.meta.env.VITE_MICROFLOW_API_BASE_URL ?? import.meta.env.MICROFLOW_API_BASE_URL ?? "/api";
  return {
    mode: contractMockEnabled ? "http" : (mode ?? "http"),
    apiBaseUrl: microflowApiBaseUrl,
    workspaceId: input.workspaceId,
    tenantId: input.tenantId,
    currentUser: input.currentUser,
    onUnauthorized: () => window.dispatchEvent(new CustomEvent("atlas:microflow-unauthorized")),
    onForbidden: () => window.dispatchEvent(new CustomEvent("atlas:microflow-forbidden")),
    onApiError: error => window.dispatchEvent(new CustomEvent("atlas:microflow-api-error", { detail: error })),
  };
}
