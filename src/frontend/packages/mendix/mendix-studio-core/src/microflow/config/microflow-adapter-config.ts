import type { MicroflowApiError } from "../contracts/api/api-envelope";

export type MicroflowAdapterMode = "mock" | "local" | "http";
export type MicroflowValidationAdapterMode = "auto" | "local" | "http";
export type MicroflowRuntimeEnvironment = "development" | "test" | "production" | "storybook" | "unknown";

export interface MicroflowAdapterRuntimePolicy {
  environment: MicroflowRuntimeEnvironment;
  defaultMode: MicroflowAdapterMode;
  allowMock: boolean;
  allowLocal: boolean;
  allowHttp: boolean;
  allowMockFallback: boolean;
  allowLocalFallback: boolean;
}

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
  runtimeEnvironment?: MicroflowRuntimeEnvironment;
  requestHeaders?: Record<string, string>;
  onUnauthorized?: () => void;
  onForbidden?: () => void;
  onApiError?: (error: MicroflowApiError) => void;
}

type RuntimeEnv = Record<string, string | boolean | undefined>;

function readImportMetaEnv(): RuntimeEnv {
  return ((import.meta as unknown as { env?: RuntimeEnv }).env ?? {});
}

export function getMicroflowRuntimeEnvironment(env: RuntimeEnv = readImportMetaEnv()): MicroflowRuntimeEnvironment {
  const mode = env.MODE ?? env.NODE_ENV;
  if (env.STORYBOOK === true || mode === "storybook") {
    return "storybook";
  }
  if (env.PROD === true || mode === "production") {
    return "production";
  }
  if (env.TEST === true || mode === "test") {
    return "test";
  }
  if (env.DEV === true || mode === "development") {
    return "development";
  }
  return "unknown";
}

export function getMicroflowAdapterRuntimePolicy(environment: MicroflowRuntimeEnvironment): MicroflowAdapterRuntimePolicy {
  switch (environment) {
    case "production":
      return { environment, defaultMode: "http", allowMock: false, allowLocal: false, allowHttp: true, allowMockFallback: false, allowLocalFallback: false };
    case "test":
      return { environment, defaultMode: "mock", allowMock: true, allowLocal: true, allowHttp: true, allowMockFallback: true, allowLocalFallback: true };
    case "storybook":
      return { environment, defaultMode: "mock", allowMock: true, allowLocal: true, allowHttp: false, allowMockFallback: true, allowLocalFallback: true };
    case "development":
      return { environment, defaultMode: "local", allowMock: true, allowLocal: true, allowHttp: true, allowMockFallback: true, allowLocalFallback: true };
    default:
      return { environment, defaultMode: "local", allowMock: true, allowLocal: true, allowHttp: true, allowMockFallback: false, allowLocalFallback: false };
  }
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
  const environment = getMicroflowRuntimeEnvironment(env);
  const policy = getMicroflowAdapterRuntimePolicy(environment);
  if (environment === "unknown" && getMicroflowApiBaseUrl(env)) {
    return "http";
  }
  return policy.defaultMode;
}

export function getMicroflowApiBaseUrl(env: RuntimeEnv = readImportMetaEnv()): string | undefined {
  const value = env.MICROFLOW_API_BASE_URL ?? env.VITE_MICROFLOW_API_BASE_URL ?? env.VITE_API_BASE;
  return typeof value === "string" && value.trim() ? value.trim() : undefined;
}

export function isProductionMicroflowEnvironment(environment: MicroflowRuntimeEnvironment = getMicroflowRuntimeEnvironment()): boolean {
  return environment === "production";
}

export function assertMicroflowAdapterAllowed(mode: MicroflowAdapterMode, policy: MicroflowAdapterRuntimePolicy): void {
  if (mode === "mock" && !policy.allowMock) {
    throw new Error("Microflow mock adapter is not allowed in production.");
  }
  if (mode === "local" && !policy.allowLocal) {
    throw new Error("Microflow local adapter is not allowed in production.");
  }
  if (mode === "http" && !policy.allowHttp) {
    throw new Error(`Microflow http adapter is not allowed in ${policy.environment}.`);
  }
}

export function shouldAllowMockFallback(config: MicroflowAdapterFactoryConfig, policy: MicroflowAdapterRuntimePolicy): boolean {
  return config.enableMockFallback === true && policy.allowMockFallback;
}

export function validateMicroflowAdapterRuntimePolicy(
  config: MicroflowAdapterFactoryConfig,
  policy: MicroflowAdapterRuntimePolicy,
): MicroflowAdapterFactoryConfig & { mode: MicroflowAdapterMode; runtimeEnvironment: MicroflowRuntimeEnvironment } {
  const mode = config.mode ?? policy.defaultMode;
  assertMicroflowAdapterAllowed(mode, policy);
  if (mode === "http" && !config.apiBaseUrl?.trim()) {
    throw new Error("微流服务未配置：HTTP adapter 需要 MICROFLOW_API_BASE_URL 或 VITE_MICROFLOW_API_BASE_URL。");
  }
  if (config.enableMockFallback && !policy.allowMockFallback) {
    throw new Error("Microflow mock fallback is not allowed in production.");
  }
  if (config.validationMode === "local" && policy.environment === "production") {
    throw new Error("Microflow local validation is not allowed in production http mode.");
  }
  return {
    ...config,
    mode,
    runtimeEnvironment: policy.environment,
    enableMockFallback: shouldAllowMockFallback(config, policy),
    enableLocalStorage: policy.allowLocal && config.enableLocalStorage !== false,
  };
}

export function validateMicroflowAdapterConfig(config: MicroflowAdapterFactoryConfig): MicroflowAdapterFactoryConfig & { mode: MicroflowAdapterMode } {
  const environment = config.runtimeEnvironment ?? getMicroflowRuntimeEnvironment();
  const policy = getMicroflowAdapterRuntimePolicy(environment);
  return validateMicroflowAdapterRuntimePolicy(config, policy);
}

export function createDefaultMicroflowAdapterConfig(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterFactoryConfig & { mode: MicroflowAdapterMode } {
  const environment = config.runtimeEnvironment ?? getMicroflowRuntimeEnvironment();
  const policy = getMicroflowAdapterRuntimePolicy(environment);
  const apiBaseUrl = config.apiBaseUrl ?? getMicroflowApiBaseUrl();
  return validateMicroflowAdapterConfig({
    ...config,
    runtimeEnvironment: environment,
    mode: config.mode ?? (environment === "unknown" && apiBaseUrl ? "http" : policy.defaultMode),
    apiBaseUrl,
  });
}
