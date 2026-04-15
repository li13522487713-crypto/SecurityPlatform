import type { AuthProfile } from "../types/api-base";

const ACCESS_TOKEN_KEY = "access_token";
const REFRESH_TOKEN_KEY = "refresh_token";
const TENANT_ID_KEY = "tenant_id";
const PROFILE_KEY = "auth_profile";
const PROJECT_ID_KEY = "project_id";
const PROJECT_SCOPE_KEY = "project_scope_enabled";

let authStorageNamespace = "atlas";

function getScopedKey(rawKey: string): string {
  return `${authStorageNamespace}_${rawKey}`;
}

function getSafeStorage(kind: "localStorage" | "sessionStorage"): Storage | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window[kind];
  } catch {
    return null;
  }
}

function safeGetItem(kind: "localStorage" | "sessionStorage", key: string): string | null {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return null;
  }
  try {
    return storage.getItem(key);
  } catch {
    return null;
  }
}

function safeSetItem(kind: "localStorage" | "sessionStorage", key: string, value: string): void {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return;
  }
  try {
    storage.setItem(key, value);
  } catch {
    // Ignore storage write failures to avoid blocking rendering.
  }
}

function safeRemoveItem(kind: "localStorage" | "sessionStorage", key: string): void {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return;
  }
  try {
    storage.removeItem(key);
  } catch {
    // Ignore storage removal failures to avoid breaking logout/cleanup.
  }
}

export function setAuthStorageNamespace(namespace: string) {
  const normalized = namespace.trim().toLowerCase();
  authStorageNamespace = normalized.length > 0 ? normalized : "atlas";
}

export const getAccessToken = () => {
  const key = getScopedKey(ACCESS_TOKEN_KEY);
  return safeGetItem("sessionStorage", key) ?? safeGetItem("localStorage", key);
};

export const setAccessToken = (token: string) => {
  const key = getScopedKey(ACCESS_TOKEN_KEY);
  safeSetItem("sessionStorage", key, token);
  safeRemoveItem("localStorage", key);
};

export const getRefreshToken = () => safeGetItem("localStorage", getScopedKey(REFRESH_TOKEN_KEY));

export const setRefreshToken = (token: string) => {
  safeSetItem("localStorage", getScopedKey(REFRESH_TOKEN_KEY), token);
};

export const getTenantId = () => safeGetItem("localStorage", getScopedKey(TENANT_ID_KEY));

export const setTenantId = (tenantId: string) => {
  safeSetItem("localStorage", getScopedKey(TENANT_ID_KEY), tenantId);
};

export const getProjectId = () => safeGetItem("localStorage", getScopedKey(PROJECT_ID_KEY));

export const setProjectId = (projectId: string) => {
  safeSetItem("localStorage", getScopedKey(PROJECT_ID_KEY), projectId);
};

export const clearProjectId = () => {
  safeRemoveItem("localStorage", getScopedKey(PROJECT_ID_KEY));
};

export const getProjectScopeEnabled = () => safeGetItem("localStorage", getScopedKey(PROJECT_SCOPE_KEY)) === "true";

export const setProjectScopeEnabled = (enabled: boolean) => {
  safeSetItem("localStorage", getScopedKey(PROJECT_SCOPE_KEY), enabled ? "true" : "false");
};

export const getAuthProfile = (): AuthProfile | null => {
  const key = getScopedKey(PROFILE_KEY);
  const raw = safeGetItem("sessionStorage", key) ?? safeGetItem("localStorage", key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthProfile;
  } catch {
    safeRemoveItem("sessionStorage", key);
    safeRemoveItem("localStorage", key);
    return null;
  }
};

export const setAuthProfile = (profile: AuthProfile) => {
  const key = getScopedKey(PROFILE_KEY);
  safeSetItem("sessionStorage", key, JSON.stringify(profile));
  safeRemoveItem("localStorage", key);
};

export const hasAuthSessionSignal = () => {
  return Boolean(getTenantId() && (getAccessToken() || getRefreshToken()));
};

export const clearAuthStorage = () => {
  safeRemoveItem("sessionStorage", getScopedKey(ACCESS_TOKEN_KEY));
  safeRemoveItem("sessionStorage", getScopedKey(PROFILE_KEY));
  safeRemoveItem("localStorage", getScopedKey(ACCESS_TOKEN_KEY));
  safeRemoveItem("localStorage", getScopedKey(REFRESH_TOKEN_KEY));
  safeRemoveItem("localStorage", getScopedKey(TENANT_ID_KEY));
  safeRemoveItem("localStorage", getScopedKey(PROFILE_KEY));
  safeRemoveItem("localStorage", getScopedKey(PROJECT_ID_KEY));
  safeRemoveItem("localStorage", getScopedKey(PROJECT_SCOPE_KEY));
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
