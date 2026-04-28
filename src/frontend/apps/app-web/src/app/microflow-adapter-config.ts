import { parseMicroflowAdapterMode, type MicroflowAdapterFactoryConfig } from "@atlas/mendix-studio-core";

function isMicroflowContractMockEnabled(): boolean {
  return (import.meta.env.VITE_MICROFLOW_API_MOCK ?? import.meta.env.MICROFLOW_API_MOCK) === "msw";
}

function resolveMicroflowApiBaseUrl(): string {
  const configured = import.meta.env.VITE_MICROFLOW_API_BASE_URL ?? import.meta.env.MICROFLOW_API_BASE_URL ?? "/api/v1";
  if (typeof window === "undefined") {
    return configured;
  }

  try {
    const target = new URL(configured, window.location.origin);
    const current = new URL(window.location.origin);
    const targetIsLoopback = target.hostname === "localhost" || target.hostname === "127.0.0.1";
    const currentIsLoopback = current.hostname === "localhost" || current.hostname === "127.0.0.1";
    if (targetIsLoopback && currentIsLoopback && target.origin !== current.origin) {
      return target.pathname.replace(/\/+$/u, "") || "/api/v1";
    }
  } catch {
    return configured;
  }

  return configured;
}

export function createAppMicroflowAdapterConfig(input: {
  workspaceId?: string;
  tenantId?: string;
  currentUser?: MicroflowAdapterFactoryConfig["currentUser"];
  requestHeaders?: Record<string, string>;
}): MicroflowAdapterFactoryConfig {
  const mode = parseMicroflowAdapterMode(import.meta.env.VITE_MICROFLOW_ADAPTER_MODE ?? import.meta.env.MICROFLOW_ADAPTER_MODE);
  const contractMockEnabled = isMicroflowContractMockEnabled();
  const microflowApiBaseUrl = resolveMicroflowApiBaseUrl();
  return {
    mode: contractMockEnabled ? "http" : (mode ?? "http"),
    apiBaseUrl: microflowApiBaseUrl,
    workspaceId: input.workspaceId,
    tenantId: input.tenantId,
    currentUser: input.currentUser,
    requestHeaders: input.requestHeaders,
    onUnauthorized: () => window.dispatchEvent(new CustomEvent("atlas:microflow-unauthorized")),
    onForbidden: () => window.dispatchEvent(new CustomEvent("atlas:microflow-forbidden")),
    onApiError: error => window.dispatchEvent(new CustomEvent("atlas:microflow-api-error", { detail: error })),
  };
}
