import type { AuthProfile } from "@/types/api";

const ACCESS_TOKEN_KEY = "access_token";
const REFRESH_TOKEN_KEY = "refresh_token";
const TENANT_ID_KEY = "tenant_id";
const PROFILE_KEY = "auth_profile";
const PROJECT_ID_KEY = "project_id";
const PROJECT_SCOPE_KEY = "project_scope_enabled";
const ANTIFORGERY_TOKEN_KEY = "antiforgery_token";

export const getAccessToken = () => sessionStorage.getItem(ACCESS_TOKEN_KEY) ?? localStorage.getItem(ACCESS_TOKEN_KEY);

export const setAccessToken = (token: string) => {
  sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
  localStorage.removeItem(ACCESS_TOKEN_KEY);
};

export const getRefreshToken = () => localStorage.getItem(REFRESH_TOKEN_KEY);

export const setRefreshToken = (token: string) => {
  localStorage.setItem(REFRESH_TOKEN_KEY, token);
};

export const getTenantId = () => localStorage.getItem(TENANT_ID_KEY);

export const setTenantId = (tenantId: string) => {
  localStorage.setItem(TENANT_ID_KEY, tenantId);
};

export const getProjectId = () => localStorage.getItem(PROJECT_ID_KEY);

export const setProjectId = (projectId: string) => {
  localStorage.setItem(PROJECT_ID_KEY, projectId);
};

export const clearProjectId = () => {
  localStorage.removeItem(PROJECT_ID_KEY);
};

export const getProjectScopeEnabled = () => localStorage.getItem(PROJECT_SCOPE_KEY) === "true";

export const setProjectScopeEnabled = (enabled: boolean) => {
  localStorage.setItem(PROJECT_SCOPE_KEY, enabled ? "true" : "false");
};

export const getAntiforgeryToken = () => sessionStorage.getItem(ANTIFORGERY_TOKEN_KEY);

export const setAntiforgeryToken = (token: string) => {
  sessionStorage.setItem(ANTIFORGERY_TOKEN_KEY, token);
};

export const clearAntiforgeryToken = () => {
  sessionStorage.removeItem(ANTIFORGERY_TOKEN_KEY);
};

export const getAuthProfile = (): AuthProfile | null => {
  const raw = sessionStorage.getItem(PROFILE_KEY) ?? localStorage.getItem(PROFILE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthProfile;
  } catch {
    sessionStorage.removeItem(PROFILE_KEY);
    localStorage.removeItem(PROFILE_KEY);
    return null;
  }
};

export const setAuthProfile = (profile: AuthProfile) => {
  sessionStorage.setItem(PROFILE_KEY, JSON.stringify(profile));
  localStorage.removeItem(PROFILE_KEY);
};

export const clearAuthStorage = () => {
  sessionStorage.removeItem(ACCESS_TOKEN_KEY);
  sessionStorage.removeItem(PROFILE_KEY);
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(TENANT_ID_KEY);
  localStorage.removeItem(PROFILE_KEY);
  localStorage.removeItem(PROJECT_ID_KEY);
  localStorage.removeItem(PROJECT_SCOPE_KEY);
  sessionStorage.removeItem(ANTIFORGERY_TOKEN_KEY);
};

export const isAdminRole = (profile: AuthProfile | null) => {
  if (!profile) return false;
  if (profile.isPlatformAdmin) return true;
  return profile.roles.some((role) => {
    const normalized = role.trim().toLowerCase();
    return normalized === "admin" || normalized === "superadmin";
  });
};

export const hasPermission = (profile: AuthProfile | null, code: string) => {
  if (!profile) return false;
  if (isAdminRole(profile)) return true;
  return profile.permissions.includes(code);
};
