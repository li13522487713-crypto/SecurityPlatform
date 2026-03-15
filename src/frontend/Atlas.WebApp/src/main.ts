import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import Antd from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import "amis/lib/themes/default.css";
import "amis/lib/helper.css";
import "amis/sdk/iconfont.css";
import "./styles/amis-overrides.css";
import "./styles/index.css";
import "./styles/approval-x6.css";
import { i18n } from "./i18n";
import { createPinia } from "pinia";
import { hasPermi, hasRole } from "@/directives/permission";
import { reportClientErrorSilently, warmupAuthSession } from "@/services/api-core";

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

await warmupAuthSession();

app.use(pinia);
app.use(router);
app.use(Antd);
app.use(i18n);
app.directive("hasPermi", hasPermi);
app.directive("hasRole", hasRole);

app.mount("#app");
