import type { ReleaseState } from "@atlas/shared-kernel";

export function createInitialReleaseState(): ReleaseState {
  return {
    status: "draft"
  };
}

export * from "./types";
export * from "./runtime-execution-tracker";
