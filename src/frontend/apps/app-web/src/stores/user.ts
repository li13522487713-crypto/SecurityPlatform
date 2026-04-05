import { defineStore } from "pinia";
import type { AuthProfile } from "@atlas/shared-core";
import {
  clearAuthStorage,
  getAccessToken,
  getAuthProfile,
  getTenantId,
  setAuthProfile
} from "@atlas/shared-core";
import {
  loginByAppEntry,
  logout as logoutApi
} from "@/services/api-auth";
import { getCurrentUser } from "@/services/api-profile";

interface AppUserState {
  isAuthenticated: boolean;
  appKey: string;
  profile: AuthProfile | null;
  roles: string[];
  permissions: string[];
  name: string;
}

let _getInfoInflight: Promise<AuthProfile> | null = null;

export const useAppUserStore = defineStore("app-user", {
  state: (): AppUserState => ({
    isAuthenticated: Boolean(getAccessToken()),
    appKey: "",
    profile: getAuthProfile(),
    roles: getAuthProfile()?.roles ?? [],
    permissions: getAuthProfile()?.permissions ?? [],
    name: getAuthProfile()?.displayName || getAuthProfile()?.username || ""
  }),

  actions: {
    setAppKey(appKey: string) {
      this.appKey = appKey;
    },

    async login(tenantId: string, username: string, password: string) {
      await loginByAppEntry(tenantId, username, password);
      this.isAuthenticated = true;
    },

    async getInfo(): Promise<AuthProfile> {
      if (_getInfoInflight) return _getInfoInflight;
      _getInfoInflight = (async () => {
        const profile = await getCurrentUser();
        this.profile = profile;
        this.roles = profile.roles ?? [];
        this.permissions = profile.permissions ?? [];
        this.name = profile.displayName || profile.username || "";
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

    checkAuth(): boolean {
      const token = getAccessToken();
      const tenantId = getTenantId();
      this.isAuthenticated = Boolean(token) && Boolean(tenantId);
      return this.isAuthenticated;
    },

    async logout() {
      try {
        await logoutApi();
      } finally {
        this.isAuthenticated = false;
        this.profile = null;
        this.roles = [];
        this.permissions = [];
        this.name = "";
        clearAuthStorage();
      }
    }
  }
});
