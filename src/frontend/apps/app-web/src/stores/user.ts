import { defineStore } from "pinia";
import {
  clearAuthStorage,
  getAccessToken,
  getTenantId
} from "@atlas/shared-core";
import {
  loginByAppEntry,
  logout as logoutApi
} from "@/services/api-auth";

interface AppUserState {
  isAuthenticated: boolean;
  appKey: string;
}

export const useAppUserStore = defineStore("app-user", {
  state: (): AppUserState => ({
    isAuthenticated: Boolean(getAccessToken()),
    appKey: ""
  }),

  actions: {
    setAppKey(appKey: string) {
      this.appKey = appKey;
    },

    async login(tenantId: string, username: string, password: string) {
      await loginByAppEntry(tenantId, username, password);
      this.isAuthenticated = true;
    },

    checkAuth(): boolean {
      const token = getAccessToken();
      const tenantId = getTenantId();
      this.isAuthenticated = Boolean(token) && Boolean(tenantId);
      return this.isAuthenticated;
    },

    async logout() {
      await logoutApi();
      this.isAuthenticated = false;
      clearAuthStorage();
    }
  }
});
