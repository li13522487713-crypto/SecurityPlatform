import { parseMicroflowAdapterMode, type MicroflowAdapterFactoryConfig } from "@atlas/mendix-studio-core";

export function createAppMicroflowAdapterConfig(input: {
  workspaceId?: string;
  tenantId?: string;
  currentUser?: MicroflowAdapterFactoryConfig["currentUser"];
}): MicroflowAdapterFactoryConfig {
  const mode = parseMicroflowAdapterMode(import.meta.env.VITE_MICROFLOW_ADAPTER_MODE ?? import.meta.env.MICROFLOW_ADAPTER_MODE);
  return {
    mode,
    apiBaseUrl: import.meta.env.VITE_MICROFLOW_API_BASE_URL ?? import.meta.env.MICROFLOW_API_BASE_URL ?? import.meta.env.VITE_API_BASE,
    workspaceId: input.workspaceId,
    tenantId: input.tenantId,
    currentUser: input.currentUser,
  };
}
