import type { ApiResponse } from "@atlas/shared-core";

export interface SetupStateResponse {
  platformStatus: string;
  platformSetupCompleted: boolean;
  appStatus: string;
  appSetupCompleted: boolean;
}

export interface AppSetupInitializeRequest {
  appName: string;
  adminUsername: string;
}

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers ?? {})
    }
  });
  return response.json() as Promise<T>;
}

export async function getSetupState(): Promise<ApiResponse<SetupStateResponse>> {
  return fetchJson<ApiResponse<SetupStateResponse>>("/api/v1/setup/state");
}

export async function initializeApp(
  request: AppSetupInitializeRequest
): Promise<ApiResponse<SetupStateResponse>> {
  return fetchJson<ApiResponse<SetupStateResponse>>("/api/v1/setup/initialize", {
    method: "POST",
    body: JSON.stringify(request)
  });
}
