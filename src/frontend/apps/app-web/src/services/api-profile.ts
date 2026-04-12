import type {
  ApiResponse,
  AuthProfile,
  ChangePasswordRequest,
  UserProfileDetail,
  UserProfileUpdateRequest,
} from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export interface MfaSetupResult {
  secretKey: string;
  provisioningUri: string;
}

export interface MfaStatusResult {
  mfaEnabled: boolean;
}

export async function getCurrentUser(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/me");
  if (!response.data) throw new Error(response.message || "Failed to fetch user info");
  return response.data;
}

export async function getProfileDetail(): Promise<UserProfileDetail> {
  const response = await requestApi<ApiResponse<UserProfileDetail>>("/auth/profile");
  if (!response.data) throw new Error(response.message || "Failed to fetch profile");
  return response.data;
}

export async function updateProfile(request: UserProfileUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/profile", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Failed to update profile");
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/password", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Failed to change password");
}

export async function getMfaStatus(): Promise<MfaStatusResult> {
  const response = await requestApi<ApiResponse<MfaStatusResult>>("/mfa/status");
  if (!response.data) throw new Error(response.message || "Failed to fetch MFA status");
  return response.data;
}

export async function setupMfa(): Promise<MfaSetupResult> {
  const response = await requestApi<ApiResponse<MfaSetupResult>>("/mfa/setup", { method: "POST" });
  if (!response.data) throw new Error(response.message || "Failed to setup MFA");
  return response.data;
}

export async function verifyMfaSetup(code: string): Promise<boolean> {
  const response = await requestApi<ApiResponse<{ mfaEnabled: boolean }>>("/mfa/verify-setup", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code })
  });
  return response.success;
}

export async function disableMfa(code: string): Promise<boolean> {
  const response = await requestApi<ApiResponse<{ mfaEnabled: boolean }>>("/mfa/disable", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code })
  });
  return response.success;
}
