import type { AuthProfile } from "../types/api-base";

const ACCESS_TOKEN_KEY = "access_token";
const REFRESH_TOKEN_KEY = "refresh_token";
const TENANT_ID_KEY = "tenant_id";
const PROFILE_KEY = "auth_profile";
const PROJECT_ID_KEY = "project_id";
const PROJECT_SCOPE_KEY = "project_scope_enabled";
const ANTIFORGERY_TOKEN_KEY = "antiforgery_token";

let authStorageNamespace = "atlas";

function getScopedKey(rawKey: string): string {
  return `${authStorageNamespace}_${rawKey}`;
}

export function setAuthStorageNamespace(namespace: string) {
  const normalized = namespace.trim().toLowerCase();
  authStorageNamespace = normalized.length > 0 ? normalized : "atlas";
}

export const getAccessToken = () => {
  const key = getScopedKey(ACCESS_TOKEN_KEY);
  return sessionStorage.getItem(key) ?? localStorage.getItem(key);
};

export const setAccessToken = (token: string) => {
  const key = getScopedKey(ACCESS_TOKEN_KEY);
  sessionStorage.setItem(key, token);
  localStorage.removeItem(key);
};

export const getRefreshToken = () => localStorage.getItem(getScopedKey(REFRESH_TOKEN_KEY));

export const setRefreshToken = (token: string) => {
  localStorage.setItem(getScopedKey(REFRESH_TOKEN_KEY), token);
};

export const getTenantId = () => localStorage.getItem(getScopedKey(TENANT_ID_KEY));

export const setTenantId = (tenantId: string) => {
  localStorage.setItem(getScopedKey(TENANT_ID_KEY), tenantId);
};

export const getProjectId = () => localStorage.getItem(getScopedKey(PROJECT_ID_KEY));

export const setProjectId = (projectId: string) => {
  localStorage.setItem(getScopedKey(PROJECT_ID_KEY), projectId);
};

export const clearProjectId = () => {
  localStorage.removeItem(getScopedKey(PROJECT_ID_KEY));
};

export const getProjectScopeEnabled = () => localStorage.getItem(getScopedKey(PROJECT_SCOPE_KEY)) === "true";

export const setProjectScopeEnabled = (enabled: boolean) => {
  localStorage.setItem(getScopedKey(PROJECT_SCOPE_KEY), enabled ? "true" : "false");
};

export const getAntiforgeryToken = () => sessionStorage.getItem(getScopedKey(ANTIFORGERY_TOKEN_KEY));

export const setAntiforgeryToken = (token: string) => {
  sessionStorage.setItem(getScopedKey(ANTIFORGERY_TOKEN_KEY), token);
};

export const clearAntiforgeryToken = () => {
  sessionStorage.removeItem(getScopedKey(ANTIFORGERY_TOKEN_KEY));
};

export const getAuthProfile = (): AuthProfile | null => {
  const key = getScopedKey(PROFILE_KEY);
  const raw = sessionStorage.getItem(key) ?? localStorage.getItem(key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthProfile;
  } catch {
    sessionStorage.removeItem(key);
    localStorage.removeItem(key);
    return null;
  }
};

export const setAuthProfile = (profile: AuthProfile) => {
  const key = getScopedKey(PROFILE_KEY);
  sessionStorage.setItem(key, JSON.stringify(profile));
  localStorage.removeItem(key);
};

export const hasAuthSessionSignal = () => {
  return Boolean(getTenantId() && (getAccessToken() || getRefreshToken()));
};

export const clearAuthStorage = () => {
  sessionStorage.removeItem(getScopedKey(ACCESS_TOKEN_KEY));
  sessionStorage.removeItem(getScopedKey(PROFILE_KEY));
  localStorage.removeItem(getScopedKey(ACCESS_TOKEN_KEY));
  localStorage.removeItem(getScopedKey(REFRESH_TOKEN_KEY));
  localStorage.removeItem(getScopedKey(TENANT_ID_KEY));
  localStorage.removeItem(getScopedKey(PROFILE_KEY));
  localStorage.removeItem(getScopedKey(PROJECT_ID_KEY));
  localStorage.removeItem(getScopedKey(PROJECT_SCOPE_KEY));
  sessionStorage.removeItem(getScopedKey(ANTIFORGERY_TOKEN_KEY));
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
