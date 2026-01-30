import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import Antd from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import "./styles/index.css";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";

// 默认租户ID：用于本地开发/体验时免输入（后端仍会校验租户头）
// 建议在 .env.local 中配置 VITE_DEFAULT_TENANT_ID
const defaultTenantId = (import.meta.env.VITE_DEFAULT_TENANT_ID as string | undefined)?.trim();
if (defaultTenantId && !localStorage.getItem("tenant_id")) {
  localStorage.setItem("tenant_id", defaultTenantId);
}

const app = createApp(App);

app.use(router);
app.use(Antd);
app.use(ElementPlus);

app.mount("#app");
