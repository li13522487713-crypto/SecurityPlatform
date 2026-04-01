import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import Antd from "ant-design-vue";
import { message } from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import "./styles/amis-overrides.css";
import "./styles/index.css";
import "./styles/approval-x6.css";
import { ensureLocaleMessages, getLocale, i18n } from "./i18n";
import { translate } from "@/i18n";
import { createPinia } from "pinia";
import { hasPermi, hasRole } from "@/directives/permission";
import { isApiAuthTerminalError, isApiNetworkError, recoverAuthSession, reportClientErrorSilently, warmupAuthSession } from "@/services/api-core";
import { useNetworkStore } from "@/stores/network";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { getAccessToken, hasAuthSessionSignal } from "@/utils/auth";

// 默认租户ID：用于本地开发/体验时免输入（后端仍会校验租户头）
// 建议在 .env.local 中配置 VITE_DEFAULT_TENANT_ID
const defaultTenantId = (import.meta.env.VITE_DEFAULT_TENANT_ID as string | undefined)?.trim();
if (defaultTenantId && !localStorage.getItem("tenant_id")) {
  localStorage.setItem("tenant_id", defaultTenantId);
}

window.addEventListener("error", (event) => {
  const target = event.filename ? `${event.filename}:${event.lineno}:${event.colno}` : undefined;
  void reportClientErrorSilently({
    message: event.message || "前端运行时错误",
    stack: event.error?.stack,
    url: window.location.href,
    component: target,
    level: "error"
  });
});

window.addEventListener("unhandledrejection", (event) => {
  const reason = event.reason;
  const message = typeof reason === "string"
    ? reason
    : (reason?.message as string | undefined) ?? "未处理 Promise 异常";
  const stack = typeof reason === "object" && reason ? (reason.stack as string | undefined) : undefined;
  void reportClientErrorSilently({
    message,
    stack,
    url: window.location.href,
    component: "unhandledrejection",
    level: "error"
  });
});

const app = createApp(App);
const pinia = createPinia();
const networkStore = useNetworkStore(pinia);
const userStore = useUserStore(pinia);
const permissionStore = usePermissionStore(pinia);

let recoverPromise: Promise<void> | null = null;
const OFFLINE_MESSAGE_KEY = "network-offline";
const RECOVER_MESSAGE_KEY = "network-recover";

async function recoverSessionAfterOnline() {
  if (recoverPromise) {
    return recoverPromise;
  }

  recoverPromise = (async () => {
    if (networkStore.recovering) {
      return;
    }

    networkStore.startRecover();
    let recovered = false;
    try {
      if (!hasAuthSessionSignal()) {
        return;
      }

      const sessionReady = await recoverAuthSession();
      if (!sessionReady && !getAccessToken()) {
        return;
      }

      const needsProfile = !userStore.profile;
      const needsRoutes = !permissionStore.routeLoaded;
      if (needsProfile || needsRoutes) {
        await Promise.all([
          needsProfile ? userStore.getInfo() : Promise.resolve(),
          needsRoutes ? permissionStore.generateRoutes() : Promise.resolve()
        ]);
        if (needsRoutes) {
          permissionStore.registerRoutes(router);
        }
      }

      recovered = true;
      message.open({
        key: RECOVER_MESSAGE_KEY,
        type: "success",
        content: translate("apiCore.networkRecovered"),
        duration: 2
      });
    } catch (error) {
      if (isApiAuthTerminalError(error)) {
        await userStore.logout({ skipRemote: true });
        void router.push({ name: "login" });
        return;
      }
      if (isApiNetworkError(error)) {
        return;
      }
      console.error("[network-recovery] 恢复失败", error);
    } finally {
      networkStore.finishRecover(recovered);
      recoverPromise = null;
    }
  })();

  return recoverPromise;
}

await ensureLocaleMessages(getLocale());

if (typeof window !== "undefined") {
  if (!navigator.onLine) {
    networkStore.markOffline();
  }

  window.addEventListener("offline", () => {
    networkStore.markOffline();
    message.open({
      key: OFFLINE_MESSAGE_KEY,
      type: "warning",
      content: translate("apiCore.networkOffline"),
      duration: 2
    });
  });

  window.addEventListener("online", () => {
    const wasOffline = networkStore.offline;
    networkStore.markOnline();
    message.destroy(OFFLINE_MESSAGE_KEY);
    if (wasOffline) {
      void recoverSessionAfterOnline();
    }
  });
}

app.use(pinia);
app.use(router);
app.use(Antd);
app.use(i18n);
app.directive("hasPermi", hasPermi);
app.directive("hasRole", hasRole);

app.mount("#app");

void warmupAuthSession();
