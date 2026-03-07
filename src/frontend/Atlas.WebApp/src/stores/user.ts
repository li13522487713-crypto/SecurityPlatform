import { defineStore } from "pinia";
import type { AuthProfile } from "@/types/api";
import {
  clearAuthStorage,
  getAuthProfile,
  setAuthProfile,
  setAccessToken,
  setRefreshToken,
  setTenantId
} from "@/utils/auth";
import { getCurrentUser, logout as logoutApi, createToken, type RequestOptions } from "@/services/api";

interface UserState {
  profile: AuthProfile | null;
  roles: string[];
  permissions: string[];
  name: string;
  avatar: string;
}

export const useUserStore = defineStore("user", {
  state: (): UserState => ({
    profile: getAuthProfile(),
    roles: getAuthProfile()?.roles ?? [],
    permissions: getAuthProfile()?.permissions ?? [],
    name: getAuthProfile()?.displayName || getAuthProfile()?.username || "",
    avatar: ""
  }),
  actions: {
    async login(tenantId: string, username: string, password: string, options?: RequestOptions, extra?: any) {
      const normalizedTenantId = tenantId.trim();
      const result = await createToken(normalizedTenantId, username, password, options, extra);
      setAccessToken(result.accessToken);
      setRefreshToken(result.refreshToken);
      setTenantId(normalizedTenantId);
    },
    async getInfo() {
      const profile = await getCurrentUser();
      this.profile = profile;
      this.roles = profile.roles ?? [];
      this.permissions = profile.permissions ?? [];
      this.name = profile.displayName || profile.username || "";
      this.avatar = ""; // 可以配置默认头像或从profile读取
      setAuthProfile(profile);
      return profile;
    },
    hydrateFromStorage() {
      const profile = getAuthProfile();
      this.profile = profile;
      this.roles = profile?.roles ?? [];
      this.permissions = profile?.permissions ?? [];
      this.name = profile?.displayName || profile?.username || "";
    },
    async logout() {
      try {
        await logoutApi();
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
