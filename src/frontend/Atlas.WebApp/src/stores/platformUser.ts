import { defineStore } from "pinia";
import type { AuthProfile } from "@/types/api";
import {
  clearAuthStorage,
  getAuthProfile,
  setAuthProfile,
  setAccessToken,
  setRefreshToken,
  setTenantId,
} from "@/utils/auth";
import {
  getCurrentUser,
  logout as logoutApi,
  createToken,
  type RequestOptions,
} from "@/services/api";

interface PlatformUserState {
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

export const usePlatformUserStore = defineStore("platformUser", {
  state: (): PlatformUserState => ({
    profile: getAuthProfile(),
    roles: getAuthProfile()?.roles ?? [],
    permissions: getAuthProfile()?.permissions ?? [],
    name: getAuthProfile()?.displayName || getAuthProfile()?.username || "",
    avatar: "",
  }),
  actions: {
    async login(
      tenantId: string,
      username: string,
      password: string,
      options?: RequestOptions,
      extra?: {
        totpCode?: string;
        captchaKey?: string;
        captchaCode?: string;
        rememberMe?: boolean;
      },
    ) {
      const normalizedTenantId = tenantId.trim();
      const result = await createToken(normalizedTenantId, username, password, options, extra);
      setAccessToken(result.accessToken);
      setRefreshToken(result.refreshToken);
      setTenantId(normalizedTenantId);
    },
    async getInfo() {
      if (_getInfoInflight) {
        return _getInfoInflight;
      }

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
    },
  },
});
