import type { RuntimeManifest } from "../types/index";

export function bootstrapRuntimeManifest(manifest: RuntimeManifest) {
  return {
    appKey: manifest.appKey,
    pageKey: manifest.pageKey,
    version: manifest.version ?? "draft"
  };
}
