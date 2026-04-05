export type PluginCategory = "General" | "FieldType" | "Validator" | "DataSource" | "FlowNode" | "GridRenderer" | "Theme";
export type PluginMarketStatus = "Draft" | "Published" | "Deprecated";
export type PluginState = "Loaded" | "Disabled" | "Unloaded" | "Failed" | "NoEntryPoint";

export interface PluginDescriptor {
  code: string;
  name: string;
  version: string;
  description: string;
  author?: string;
  iconUrl?: string;
  category: PluginCategory;
  dependencies: PluginDependency[];
  requiredPermissions: string[];
  configSchema?: string;
  assemblyName: string;
  filePath: string;
  state: PluginState;
  loadedAt: string;
  errorMessage?: string;
}

export interface PluginDependency {
  code: string;
  minVersion?: string;
  maxVersion?: string;
}

export interface PluginMarketEntry {
  id: number;
  code: string;
  name: string;
  description: string;
  author: string;
  category: PluginCategory;
  latestVersion: string;
  downloads: number;
  iconUrl?: string;
  packageUrl?: string;
  status: PluginMarketStatus;
  publishedAt: string;
  createdAt: string;
  updatedAt: string;
  tenantId: string;
}

export interface PluginMarketVersion {
  id: number;
  entryId: number;
  version: string;
  releaseNotes?: string;
  packageHash?: string;
  packageUrl?: string;
  publishedAt: string;
}

export interface PluginMarketSearchResult {
  pageIndex: number;
  pageSize: number;
  total: number;
  items: PluginMarketEntry[];
}

export interface PublishPluginRequest {
  code: string;
  name: string;
  description: string;
  author: string;
  category: PluginCategory;
  version: string;
  iconUrl?: string;
  packageUrl?: string;
  releaseNotes?: string;
}
