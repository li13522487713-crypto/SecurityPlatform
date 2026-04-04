import { createApp } from "vue";
import Antd from "ant-design-vue";
import { createPinia } from "pinia";
import type { Router } from "vue-router";
import EntryShell from "@/EntryShell.vue";
import { ensureLocaleMessages, getLocale, i18n } from "@/i18n";
import { hasPermi, hasRole } from "@/directives/permission";
import "ant-design-vue/dist/reset.css";
import "@/styles/amis-overrides.css";
import "@/styles/index.css";
import "@/styles/approval-x6.css";

export async function bootstrapEntry(router: Router) {
  const defaultTenantId = (import.meta.env.VITE_DEFAULT_TENANT_ID as string | undefined)?.trim();
  if (defaultTenantId && !localStorage.getItem("tenant_id")) {
    localStorage.setItem("tenant_id", defaultTenantId);
  }

  await ensureLocaleMessages(getLocale());

  const app = createApp(EntryShell);
  app.use(createPinia());
  app.use(router);
  app.use(Antd);
  app.use(i18n);
  app.directive("hasPermi", hasPermi);
  app.directive("hasRole", hasRole);
  app.mount("#app");
}
