import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export type EditorResourceType = "app" | "workflow" | "agent" | "microflow";

export interface EditorWorkspaceResolution {
  resourceType: EditorResourceType;
  resourceId: string;
  workspaceId: string;
}

export class EditorWorkspaceResolutionError extends Error {
  code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = "EditorWorkspaceResolutionError";
    this.code = code;
  }
}

export async function resolveEditorWorkspace(
  resourceType: EditorResourceType,
  resourceId: string
): Promise<EditorWorkspaceResolution> {
  if (resourceType === "microflow") {
    const workspaceId = typeof window === "undefined"
      ? ""
      : window.localStorage.getItem("atlas_last_workspace_id") ?? "";
    if (!workspaceId) {
      throw new EditorWorkspaceResolutionError(
        "EDITOR_CONTEXT_WORKSPACE_UNRESOLVED",
        "Microflow editor requires a recently selected workspace."
      );
    }
    return {
      resourceType,
      resourceId,
      workspaceId
    };
  }

  const query = new URLSearchParams({
    resourceType,
    resourceId
  });
  const response = await requestApi<ApiResponse<EditorWorkspaceResolution>>(
    `/editor-context/workspace?${query.toString()}`,
    undefined,
    {
      suppressErrorMessage: true
    }
  );

  if (!response.success || !response.data) {
    throw new EditorWorkspaceResolutionError(
      response.code || "EDITOR_CONTEXT_RESOLVE_FAILED",
      response.message || "Failed to resolve editor workspace."
    );
  }

  return response.data;
}
