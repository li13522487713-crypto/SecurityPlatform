import type { AuthProfile } from "@/types/api";

const ACCESS_TOKEN_KEY = "access_token";
const TENANT_ID_KEY = "tenant_id";
const PROFILE_KEY = "auth_profile";

export const getAccessToken = () => localStorage.getItem(ACCESS_TOKEN_KEY);

export const setAccessToken = (token: string) => {
  localStorage.setItem(ACCESS_TOKEN_KEY, token);
};

export const getTenantId = () => localStorage.getItem(TENANT_ID_KEY);

export const setTenantId = (tenantId: string) => {
  localStorage.setItem(TENANT_ID_KEY, tenantId);
};

export const getAuthProfile = (): AuthProfile | null => {
  const raw = localStorage.getItem(PROFILE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthProfile;
  } catch {
    localStorage.removeItem(PROFILE_KEY);
    return null;
  }
};

export const setAuthProfile = (profile: AuthProfile) => {
  localStorage.setItem(PROFILE_KEY, JSON.stringify(profile));
};

export const clearAuthStorage = () => {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(TENANT_ID_KEY);
  localStorage.removeItem(PROFILE_KEY);
};

export const isAdminRole = (profile: AuthProfile | null) => {
  if (!profile) return false;
  return profile.roles.some((role) => role.toLowerCase() === "admin");
};

export const hasPermission = (profile: AuthProfile | null, code: string) => {
  if (!profile) return false;
  if (isAdminRole(profile)) return true;
  return profile.permissions.includes(code);
};
