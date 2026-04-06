import type { ApiResponse } from "@atlas/shared-core";

export interface SetupStateResponse {
  status: string;
  platformSetupCompleted: boolean;
}

export async function getSetupState(): Promise<ApiResponse<SetupStateResponse>> {
  const resp = await fetch("/api/v1/setup/state", {
    headers: { "Content-Type": "application/json" }
  });
  return resp.json() as Promise<ApiResponse<SetupStateResponse>>;
}
