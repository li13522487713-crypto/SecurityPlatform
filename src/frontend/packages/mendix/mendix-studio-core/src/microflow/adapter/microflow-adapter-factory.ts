import { createLocalMicroflowApiClient, type MicroflowApiClient as RuntimeMicroflowApiClient } from "@atlas/microflow";
import type { MicroflowMetadataAdapter } from "@atlas/microflow/metadata";
import { createLocalMicroflowMetadataAdapter, createMockMicroflowMetadataAdapter } from "@atlas/microflow/metadata";

import type { MicroflowAdapterFactoryConfig, MicroflowAdapterMode, MicroflowAdapterRuntimePolicy } from "../config/microflow-adapter-config";
import { createDefaultMicroflowAdapterConfig, validateMicroflowAdapterConfig } from "../config/microflow-adapter-config";
import { getMicroflowAdapterRuntimePolicy, getMicroflowRuntimeEnvironment } from "../config/microflow-adapter-config";
import { createHttpMicroflowMetadataAdapter } from "../metadata/http-metadata-adapter";
import { createHttpMicroflowResourceAdapter } from "./http/http-resource-adapter";
import { createHttpMicroflowRuntimeAdapter } from "./http/http-runtime-adapter";
import { MicroflowApiClient } from "./http/microflow-api-client";
import { createLocalMicroflowResourceAdapter } from "./local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "./microflow-resource-adapter";
import { createMockMicroflowResourceAdapter } from "./mock-microflow-resource-adapter";
import { createHttpMicroflowValidationAdapter, createLocalMicroflowValidationAdapter, type MicroflowValidationAdapter } from "./microflow-validation-adapter";

export interface MicroflowAdapterBundle {
  mode: MicroflowAdapterMode;
  apiBaseUrl?: string;
  runtimePolicy: MicroflowAdapterRuntimePolicy;
  resourceAdapter: MicroflowResourceAdapter;
  metadataAdapter: MicroflowMetadataAdapter;
  runtimeAdapter: RuntimeMicroflowApiClient;
  validationAdapter?: MicroflowValidationAdapter;
  apiClient?: MicroflowApiClient;
  dispose?: () => void;
}

function resolvePolicy(config: MicroflowAdapterFactoryConfig): MicroflowAdapterRuntimePolicy {
  return getMicroflowAdapterRuntimePolicy(config.runtimeEnvironment ?? getMicroflowRuntimeEnvironment());
}

function toLocalOptions(config: MicroflowAdapterFactoryConfig) {
  return {
    workspaceId: config.workspaceId,
    currentUser: config.currentUser,
    enableLocalStorage: config.enableLocalStorage,
  };
}

export function createMockMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterBundle {
  const resolved = validateMicroflowAdapterConfig({ ...config, mode: "mock" });
  return {
    mode: "mock",
    apiBaseUrl: resolved.apiBaseUrl,
    runtimePolicy: resolvePolicy(resolved),
    resourceAdapter: createMockMicroflowResourceAdapter(toLocalOptions(resolved)),
    metadataAdapter: createMockMicroflowMetadataAdapter(),
    runtimeAdapter: createLocalMicroflowApiClient(),
    validationAdapter: createLocalMicroflowValidationAdapter(),
  };
}

export function createLocalMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterBundle {
  const resolved = validateMicroflowAdapterConfig({ ...config, mode: "local" });
  return {
    mode: "local",
    apiBaseUrl: resolved.apiBaseUrl,
    runtimePolicy: resolvePolicy(resolved),
    resourceAdapter: createLocalMicroflowResourceAdapter(toLocalOptions(resolved)),
    metadataAdapter: createLocalMicroflowMetadataAdapter(),
    runtimeAdapter: createLocalMicroflowApiClient(),
    validationAdapter: createLocalMicroflowValidationAdapter(),
  };
}

export function createHttpMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig): MicroflowAdapterBundle {
  const resolved = validateMicroflowAdapterConfig({ ...config, mode: "http" });
  if (resolved.enableMockFallback) {
    console.warn("Microflow mock fallback is enabled for development only; HTTP adapter errors will still be surfaced.");
  }
  const apiClient = new MicroflowApiClient(resolved);
  const validationAdapter = resolved.validationMode === "local"
    ? createLocalMicroflowValidationAdapter()
    : createHttpMicroflowValidationAdapter({ ...resolved, apiClient });
  return {
    mode: "http",
    apiBaseUrl: resolved.apiBaseUrl,
    runtimePolicy: resolvePolicy(resolved),
    apiClient,
    resourceAdapter: createHttpMicroflowResourceAdapter({ ...resolved, apiClient }),
    metadataAdapter: createHttpMicroflowMetadataAdapter({ ...resolved, apiClient }),
    runtimeAdapter: createHttpMicroflowRuntimeAdapter({ ...resolved, apiClient }),
    validationAdapter,
  };
}

export function createMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterBundle {
  const resolved = createDefaultMicroflowAdapterConfig(config);
  if (resolved.mode === "mock") {
    return createMockMicroflowAdapterBundle(resolved);
  }
  if (resolved.mode === "local") {
    return createLocalMicroflowAdapterBundle(resolved);
  }
  return createHttpMicroflowAdapterBundle(resolved);
}
