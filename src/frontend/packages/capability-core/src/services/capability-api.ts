import type { CapabilityManifest } from "../types/index";

interface ApiEnvelope<T> {
  success: boolean;
  code: string;
  message: string;
  traceId?: string;
  data: T;
}

export type RequestApi = <T>(
  path: string,
  init?: RequestInit
) => Promise<T>;

export interface CapabilityApi {
  list: () => Promise<CapabilityManifest[]>;
  getByKey: (capabilityKey: string) => Promise<CapabilityManifest | null>;
}

export function createCapabilityApi(requestApi: RequestApi): CapabilityApi {
  return {
    async list() {
      const response = await requestApi<ApiEnvelope<CapabilityManifest[]>>(
        "/api/v2/capabilities"
      );
      if (!response.success) {
        throw new Error(response.message || "Load capability list failed.");
      }

      return response.data ?? [];
    },
    async getByKey(capabilityKey: string) {
      const normalized = capabilityKey.trim();
      if (!normalized) {
        return null;
      }

      try {
        const response = await requestApi<ApiEnvelope<CapabilityManifest>>(
          `/api/v2/capabilities/${encodeURIComponent(normalized)}`
        );
        if (!response.success) {
          throw new Error(response.message || "Load capability failed.");
        }
        return response.data ?? null;
      } catch {
        return null;
      }
    }
  };
}
