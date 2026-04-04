import { defineStore } from "pinia";
import type { AuthProfile } from "@atlas/shared-core";
import {
  clearAuthStorage,
  getAuthProfile,
  setAuthProfile,
  setAccessToken,
  setRefreshToken,
  setTenantId
} from "@atlas/shared-core";
import { getCurrentUser, logout as logoutApi, createToken } from "@/services/api-auth";
import type { RequestOptions } from "@/services/api-core";

interface UserState {
  profile: AuthProfile | null;
  roles: string[];
  permissions: string[];
  name: string;
  avatar: string;
}

interface LogoutOptions {
  skipRemote?: boolean;
}

let _getInfoInflight: Promise<AuthProfile> | null = null;

export const useUserStore = defineStore("user", {
  state: (): UserState => ({
    profile: getAuthProfile(),
    roles: getAuthProfile()?.roles ?? [],
    permissions: getAuthProfile()?.permissions ?? [],
    name: getAuthProfile()?.displayName || getAuthProfile()?.username || "",
    avatar: ""
  }),
  actions: {
    async login(
      tenantId: string,
      username: string,
      password: string,
      options?: RequestOptions,
      extra?: {
        rememberMe?: boolean;
        captchaKey?: string;
        captchaCode?: string;
        totpCode?: string;
      }
    ) {
      const normalized = tenantId.trim();
      const result = await createToken(normalized, username, password, options, extra);
      setAccessToken(result.accessToken);
      setRefreshToken(result.refreshToken);
      setTenantId(normalized);
    },
    async getInfo() {
      if (_getInfoInflight) return _getInfoInflight;
      _getInfoInflight = (async () => {
        const profile = await getCurrentUser();
        this.profile = profile;
        this.roles = profile.roles ?? [];
        this.permissions = profile.permissions ?? [];
        this.name = profile.displayName || profile.username || "";
        this.avatar = "";
        setAuthProfile(profile);
        return profile;
      })();
      try {
        return await _getInfoInflight;
      } finally {
        _getInfoInflight = null;
      }
    },
    hydrateFromStorage() {
      const profile = getAuthProfile();
      this.profile = profile;
      this.roles = profile?.roles ?? [];
      this.permissions = profile?.permissions ?? [];
      this.name = profile?.displayName || profile?.username || "";
    },
    async logout(options?: LogoutOptions) {
      try {
        if (!options?.skipRemote) {
          await logoutApi();
        }
      } finally {
        this.fedLogOut();
      }
    },
    fedLogOut() {
      clearAuthStorage();
      this.profile = null;
      this.roles = [];
      this.permissions = [];
      this.name = "";
      this.avatar = "";
    }
  }
});
