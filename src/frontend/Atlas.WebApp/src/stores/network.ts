import { defineStore } from "pinia";

interface NetworkState {
  offline: boolean;
  recovering: boolean;
  lastRecoveredAt: number | null;
  lastOfflineAt: number | null;
}

export const useNetworkStore = defineStore("network", {
  state: (): NetworkState => ({
    offline: typeof navigator !== "undefined" ? !navigator.onLine : false,
    recovering: false,
    lastRecoveredAt: null,
    lastOfflineAt: null
  }),
  actions: {
    markOffline() {
      this.offline = true;
      this.lastOfflineAt = Date.now();
    },
    markOnline() {
      this.offline = false;
    },
    startRecover() {
      this.recovering = true;
    },
    finishRecover(success: boolean) {
      this.recovering = false;
      if (success) {
        this.lastRecoveredAt = Date.now();
      }
    }
  }
});
