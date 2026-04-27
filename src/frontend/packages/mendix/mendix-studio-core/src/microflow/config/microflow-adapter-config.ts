import type { MicroflowApiError } from "../contracts/api/api-envelope";

export type MicroflowAdapterMode = "mock" | "local" | "http";
export type MicroflowValidationAdapterMode = "auto" | "local" | "http";

export interface MicroflowAdapterFactoryConfig {
  mode?: MicroflowAdapterMode;
  apiBaseUrl?: string;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: {
    id: string;
    name: string;
    roles?: string[];
  };
  enableLocalStorage?: boolean;
  enableMockFallback?: boolean;
  validationMode?: MicroflowValidationAdapterMode;
  requestHeaders?: Record<string, string>;
  onUnauthorized?: () => void;
  onForbidden?: () => void;
  onApiError?: (error: MicroflowApiError) => void;
}

type RuntimeEnv = Record<string, string | boolean | undefined>;

function readImportMetaEnv(): RuntimeEnv {
  return ((import.meta as unknown as { env?: RuntimeEnv }).env ?? {});
}

export function parseMicroflowAdapterMode(value: unknown): MicroflowAdapterMode | undefined {
  if (value === "mock" || value === "local" || value === "http") {
    return value;
  }
  return undefined;
}

export function getDefaultMicroflowAdapterMode(env: RuntimeEnv = readImportMetaEnv()): MicroflowAdapterMode {
  const explicit = parseMicroflowAdapterMode(env.MICROFLOW_ADAPTER_MODE ?? env.VITE_MICROFLOW_ADAPTER_MODE);
  if (explicit) {
    return explicit;
  }
  const isProduction = env.PROD === true || env.MODE === "production" || env.NODE_ENV === "production";
  return isProduction ? "http" : "local";
}

export function getMicroflowApiBaseUrl(env: RuntimeEnv = readImportMetaEnv()): string | undefined {
  const value = env.MICROFLOW_API_BASE_URL ?? env.VITE_MICROFLOW_API_BASE_URL ?? env.VITE_API_BASE;
  return typeof value === "string" && value.trim() ? value.trim() : undefined;
}

export function validateMicroflowAdapterConfig(config: MicroflowAdapterFactoryConfig): MicroflowAdapterFactoryConfig & { mode: MicroflowAdapterMode } {
  const mode = config.mode ?? getDefaultMicroflowAdapterMode();
  if (mode === "http" && !config.apiBaseUrl?.trim()) {
    throw new Error("Microflow http adapter requires MICROFLOW_API_BASE_URL or VITE_MICROFLOW_API_BASE_URL.");
  }
  if (mode !== "http" && config.enableMockFallback) {
    return { ...config, mode };
  }
  return { ...config, mode, enableMockFallback: config.enableMockFallback === true && mode === "http" };
}

export function createDefaultMicroflowAdapterConfig(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterFactoryConfig & { mode: MicroflowAdapterMode } {
  return validateMicroflowAdapterConfig({
    ...config,
    mode: config.mode ?? getDefaultMicroflowAdapterMode(),
    apiBaseUrl: config.apiBaseUrl ?? getMicroflowApiBaseUrl(),
  });
}
