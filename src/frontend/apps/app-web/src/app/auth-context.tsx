import { createContext, useCallback, useContext, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import type { AuthProfile } from "@atlas/shared-react-core/types";
import {
  clearAuthStorage,
  getAccessToken,
  getAuthProfile,
  getTenantId,
  setAuthProfile
} from "@atlas/shared-react-core/utils";
import { loginByAppEntry, logout as logoutApi } from "../services/api-auth";
import { getCurrentUser } from "../services/api-profile";
import { rememberConfiguredAppKey } from "../services/api-core";

interface AuthContextValue {
  isAuthenticated: boolean;
  loading: boolean;
  profile: AuthProfile | null;
  permissions: string[];
  roles: string[];
  login: (
    appKey: string,
    tenantId: string,
    username: string,
    password: string,
    totpCode?: string,
    captchaKey?: string,
    captchaCode?: string
  ) => Promise<void>;
  ensureProfile: () => Promise<AuthProfile | null>;
  logout: () => Promise<void>;
  hasPermission: (permission?: string) => boolean;
}

const privilegedRoleCodes = new Set(["admin", "superadmin", "securityadmin", "systemadmin"]);
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<AuthProfile | null>(getAuthProfile());
  const [loading, setLoading] = useState(false);
  const ensureProfileRequestRef = useRef<Promise<AuthProfile | null> | null>(null);

  const roles = profile?.roles ?? [];
  const permissions = profile?.permissions ?? [];
  const isAuthenticated = Boolean(getAccessToken() && getTenantId());

  const ensureProfile = useCallback(async () => {
    if (!getAccessToken() || !getTenantId()) {
      setProfile(null);
      return null;
    }

    if (profile) {
      return profile;
    }

    if (ensureProfileRequestRef.current) {
      return ensureProfileRequestRef.current;
    }

    setLoading(true);
    const request = (async () => {
      try {
        const nextProfile = await getCurrentUser();
        setAuthProfile(nextProfile);
        setProfile(nextProfile);
        return nextProfile;
      } catch {
        clearAuthStorage();
        setProfile(null);
        return null;
      } finally {
        ensureProfileRequestRef.current = null;
        setLoading(false);
      }
    })();

    ensureProfileRequestRef.current = request;
    return request;
  }, [profile]);

  const login = useCallback(async (
    appKey: string,
    tenantId: string,
    username: string,
    password: string,
    totpCode?: string,
    captchaKey?: string,
    captchaCode?: string
  ) => {
    setLoading(true);
    try {
      rememberConfiguredAppKey(appKey);
      await loginByAppEntry(tenantId, username, password, totpCode, captchaKey, captchaCode);
      const nextProfile = await getCurrentUser();
      setAuthProfile(nextProfile);
      setProfile(nextProfile);
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    setLoading(true);
    try {
      await logoutApi();
    } finally {
      ensureProfileRequestRef.current = null;
      clearAuthStorage();
      setProfile(null);
      setLoading(false);
    }
  }, []);

  const hasPermission = useCallback((permission?: string) => {
    if (!permission) {
      return true;
    }

    if (!profile) {
      return false;
    }

    if (profile.isPlatformAdmin) {
      return true;
    }

    if (permissions.includes("*:*:*")) {
      return true;
    }

    if (roles.some(role => privilegedRoleCodes.has(role.trim().toLowerCase()))) {
      return true;
    }

    return permissions.includes(permission);
  }, [permissions, profile, roles]);

  const value = useMemo<AuthContextValue>(() => ({
    isAuthenticated,
    loading,
    profile,
    permissions,
    roles,
    login,
    ensureProfile,
    logout,
    hasPermission
  }), [ensureProfile, hasPermission, isAuthenticated, loading, login, logout, permissions, profile, roles]);

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("AuthProvider is missing.");
  }

  return context;
}

/**
 * 在某些壳子（例如单元测试中没有挂 AuthProvider 的边界）下需要可选读取 AuthContext，
 * 提供 `useOptionalAuth` 避免抛错破坏渲染顺序。
 */
export function useOptionalAuth(): AuthContextValue | null {
  return useContext(AuthContext);
}
