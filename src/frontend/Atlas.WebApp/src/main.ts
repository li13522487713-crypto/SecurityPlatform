import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import Antd from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import "amis/lib/themes/default.css";
import "amis/lib/helper.css";
import "amis/sdk/iconfont.css";
import "./styles/index.css";
import "./styles/approval-x6.css";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";
import { i18n } from "./i18n";

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
app.use(i18n);

app.mount("#app");
