import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "./api-core";
import type { LicenseStatus, LicenseActivateResult } from "@/types/api";

interface ApiRequestErrorLike extends Error {
  payload?: {
    code?: string;
    message?: string;
    traceId?: string;
  } | null;
}

export async function getLicenseStatus(): Promise<LicenseStatus> {
  const resp = await requestApi<ApiResponse<LicenseStatus>>(
    "/license/status",
    { method: "GET" },
    { disableAutoRefresh: true, suppressErrorMessage: true }
  );
  return (
    resp.data ?? {
      status: "None" as const,
      edition: "Trial" as const,
      isPermanent: false,
      issuedAt: null,
      expiresAt: null,
      remainingDays: null,
      machineBound: false,
      machineMatched: false,
      features: {},
      limits: {},
      tenantId: null,
      tenantName: null
    }
  );
}

export async function activateLicense(
  licenseContent: string
): Promise<ApiResponse<LicenseActivateResult>> {
  try {
    return await requestApi<ApiResponse<LicenseActivateResult>>(
      "/license/activate",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ licenseContent })
      },
      { disableAutoRefresh: true, suppressErrorMessage: true }
    );
  } catch (error) {
    const reqErr = error as ApiRequestErrorLike;
    const payload = reqErr?.payload ?? null;
    return {
      success: false,
      code: payload?.code ?? "LICENSE_ACTIVATE_FAILED",
      message: payload?.message ?? reqErr?.message ?? "证书激活失败",
      traceId: payload?.traceId ?? ""
    };
  }
}
