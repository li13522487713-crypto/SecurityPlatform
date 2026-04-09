export interface CapabilityNavigationSuggestion {
  group?: string;
  order?: number;
}

export interface CapabilityManifest {
  capabilityKey: string;
  title: string;
  category: string;
  hostModes: string[];
  platformRoute?: string;
  appRoute?: string;
  requiredPermissions: string[];
  navigation: CapabilityNavigationSuggestion;
  supportsExposure: boolean;
  supportedCommands: string[];
  isEnabled: boolean;
}
