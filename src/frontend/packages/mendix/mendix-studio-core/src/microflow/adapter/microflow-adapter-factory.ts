import { createLocalMicroflowApiClient, type MicroflowApiClient as RuntimeMicroflowApiClient } from "@atlas/microflow";
import type { MicroflowMetadataAdapter } from "@atlas/microflow/metadata";
import { createLocalMicroflowMetadataAdapter, createMockMicroflowMetadataAdapter } from "@atlas/microflow/metadata";

import type { MicroflowAdapterFactoryConfig, MicroflowAdapterMode } from "../config/microflow-adapter-config";
import { createDefaultMicroflowAdapterConfig, validateMicroflowAdapterConfig } from "../config/microflow-adapter-config";
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
  resourceAdapter: MicroflowResourceAdapter;
  metadataAdapter: MicroflowMetadataAdapter;
  runtimeAdapter: RuntimeMicroflowApiClient;
  validationAdapter?: MicroflowValidationAdapter;
  apiClient?: MicroflowApiClient;
  dispose?: () => void;
}

function toLocalOptions(config: MicroflowAdapterFactoryConfig) {
  return {
    workspaceId: config.workspaceId,
    currentUser: config.currentUser,
    enableLocalStorage: config.enableLocalStorage,
  };
}

export function createMockMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterBundle {
  return {
    mode: "mock",
    resourceAdapter: createMockMicroflowResourceAdapter(toLocalOptions(config)),
    metadataAdapter: createMockMicroflowMetadataAdapter(),
    runtimeAdapter: createLocalMicroflowApiClient(),
    validationAdapter: createLocalMicroflowValidationAdapter(),
  };
}

export function createLocalMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig = {}): MicroflowAdapterBundle {
  return {
    mode: "local",
    resourceAdapter: createLocalMicroflowResourceAdapter(toLocalOptions(config)),
    metadataAdapter: createLocalMicroflowMetadataAdapter(),
    runtimeAdapter: createLocalMicroflowApiClient(),
    validationAdapter: createLocalMicroflowValidationAdapter(),
  };
}

export function createHttpMicroflowAdapterBundle(config: MicroflowAdapterFactoryConfig): MicroflowAdapterBundle {
  const resolved = validateMicroflowAdapterConfig({ ...config, mode: "http" });
  const apiClient = new MicroflowApiClient(resolved);
  const validationAdapter = resolved.validationMode === "local"
    ? createLocalMicroflowValidationAdapter()
    : createHttpMicroflowValidationAdapter({ ...resolved, apiClient });
  return {
    mode: "http",
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
