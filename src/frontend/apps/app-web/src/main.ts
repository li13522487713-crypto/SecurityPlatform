import { createApp } from "vue";
import { createPinia } from "pinia";
import Antd from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import { setAuthStorageNamespace } from "@atlas/shared-core";

import App from "./App.vue";
import { router } from "./router";
import { i18n } from "./i18n";
import "./echarts";
import { suppressBenignBrowserErrors } from "./bootstrap/suppress-benign-browser-errors";

setAuthStorageNamespace("atlas_app");
suppressBenignBrowserErrors();

const app = createApp(App);
app.use(createPinia());
app.use(router);
app.use(Antd);
app.use(i18n);
app.mount("#app");
