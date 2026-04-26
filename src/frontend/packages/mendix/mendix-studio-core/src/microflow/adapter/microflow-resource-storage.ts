import type { MicroflowReference } from "../references/microflow-reference-types";
import type { MicroflowResource } from "../resource/resource-types";
import type { MicroflowPublishedSnapshot, MicroflowVersionSummary } from "../versions/microflow-version-types";

export interface StoredMicroflowResources {
  resources: MicroflowResource[];
  versions: Record<string, MicroflowVersionSummary[]>;
  snapshots?: Record<string, MicroflowPublishedSnapshot>;
  references?: Record<string, MicroflowReference[]>;
}

export const MICROFLOW_RESOURCE_STORAGE_KEY = "mendix.microflow.resources";
export const MICROFLOW_VERSION_STORAGE_KEY = "mendix.microflow.versions";
export const MICROFLOW_SNAPSHOT_STORAGE_KEY = "mendix.microflow.snapshots";
export const MICROFLOW_REFERENCE_STORAGE_KEY = "mendix.microflow.references";
const LEGACY_MICROFLOW_RESOURCE_STORAGE_KEY = "atlas_mendix_microflow_resources_v1";

export function readStoredMicroflowResources(storageKey = MICROFLOW_RESOURCE_STORAGE_KEY): StoredMicroflowResources | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }
  try {
    const raw = window.localStorage.getItem(storageKey) ?? window.localStorage.getItem(LEGACY_MICROFLOW_RESOURCE_STORAGE_KEY);
    if (!raw) {
      return undefined;
    }
    const parsed = JSON.parse(raw) as StoredMicroflowResources;
    if (!Array.isArray(parsed.resources)) {
      return undefined;
    }
    return {
      resources: parsed.resources,
      versions: parsed.versions ?? {},
      snapshots: parsed.snapshots ?? {},
      references: parsed.references ?? {}
    };
  } catch {
    return undefined;
  }
}

export function writeStoredMicroflowResources(value: StoredMicroflowResources, storageKey = MICROFLOW_RESOURCE_STORAGE_KEY): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(value));
    window.localStorage.setItem(MICROFLOW_VERSION_STORAGE_KEY, JSON.stringify(value.versions));
    window.localStorage.setItem(MICROFLOW_SNAPSHOT_STORAGE_KEY, JSON.stringify(value.snapshots ?? {}));
    window.localStorage.setItem(MICROFLOW_REFERENCE_STORAGE_KEY, JSON.stringify(value.references ?? {}));
  } catch {
    // Private browsing or storage quota failures should not break the in-memory adapter.
  }
}
