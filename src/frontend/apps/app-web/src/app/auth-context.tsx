import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { AuthProfile } from "@atlas/shared-react-core/types";
import {
  clearAuthStorage,
  getAccessToken,
  getAuthProfile,
  getTenantId,
  setAuthProfile
} from "@atlas/shared-react-core/utils";
import { loginByAppEntry, logout as logoutApi } from "@/services/api-auth";
import { getCurrentUser } from "@/services/api-profile";
import { rememberConfiguredAppKey } from "@/services/api-core";

interface AuthContextValue {
  isAuthenticated: boolean;
  loading: boolean;
  profile: AuthProfile | null;
  permissions: string[];
  roles: string[];
  login: (appKey: string, tenantId: string, username: string, password: string, totpCode?: string) => Promise<void>;
  ensureProfile: () => Promise<AuthProfile | null>;
  logout: () => Promise<void>;
  hasPermission: (permission?: string) => boolean;
}

const privilegedRoleCodes = new Set(["admin", "superadmin", "securityadmin", "systemadmin"]);
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<AuthProfile | null>(getAuthProfile());
  const [loading, setLoading] = useState(false);

  const roles = profile?.roles ?? [];
  const permissions = profile?.permissions ?? [];

  const ensureProfile = async () => {
    if (!getAccessToken() || !getTenantId()) {
      setProfile(null);
      return null;
    }

    if (profile) {
      return profile;
    }

    setLoading(true);
    try {
      const nextProfile = await getCurrentUser();
      setAuthProfile(nextProfile);
      setProfile(nextProfile);
      return nextProfile;
    } finally {
      setLoading(false);
    }
  };

  const login = async (
    appKey: string,
    tenantId: string,
    username: string,
    password: string,
    totpCode?: string
  ) => {
    setLoading(true);
    try {
      rememberConfiguredAppKey(appKey);
      await loginByAppEntry(tenantId, username, password, totpCode);
      const nextProfile = await getCurrentUser();
      setAuthProfile(nextProfile);
      setProfile(nextProfile);
    } finally {
      setLoading(false);
    }
  };

  const logout = async () => {
    setLoading(true);
    try {
      await logoutApi();
    } finally {
      clearAuthStorage();
      setProfile(null);
      setLoading(false);
    }
  };

  const value = useMemo<AuthContextValue>(() => ({
    isAuthenticated: Boolean(getAccessToken() && getTenantId()),
    loading,
    profile,
    permissions,
    roles,
    login,
    ensureProfile,
    logout,
    hasPermission: (permission?: string) => {
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
    }
  }), [loading, permissions, profile, roles]);

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
