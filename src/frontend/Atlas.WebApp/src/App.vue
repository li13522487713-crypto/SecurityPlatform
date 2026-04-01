<template>
  <a-config-provider :locale="antdLocale" :theme="appTheme">
    <router-view v-if="isAuthPage" />
    <component :is="activeLayout" v-else />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, watch } from "vue";
import { useRoute } from "vue-router";
import { getActiveLocale, getAntdLocale } from "@/i18n";
import { applyDocumentTitle } from "@/utils/i18n-navigation";

const route = useRoute();
const MainLayout = defineAsyncComponent(() => import("@/layouts/MainLayout.vue"));
const ConsoleLayout = defineAsyncComponent(() => import("@/layouts/ConsoleLayout.vue"));
const AppWorkspaceLayout = defineAsyncComponent(() => import("@/layouts/AppWorkspaceLayout.vue"));
const RuntimeLayout = defineAsyncComponent(() => import("@/layouts/RuntimeLayout.vue"));

const isAuthPage = computed(() => route.path === "/login" || route.path === "/register");
const isConsoleRoute = computed(() => route.path === "/console" || route.path.startsWith("/console/"));
const isAppWorkspaceRoute = computed(() => route.path.startsWith("/apps/"));
const isRuntimeRoute = computed(() => route.path.startsWith("/r/"));
const activeLayout = computed(() => {
  if (isConsoleRoute.value) {
    return ConsoleLayout;
  }
  if (isAppWorkspaceRoute.value) {
    return AppWorkspaceLayout;
  }
  if (isRuntimeRoute.value) {
    return RuntimeLayout;
  }
  return MainLayout;
});
const currentLocale = computed(() => getActiveLocale());
const antdLocale = computed(() => getAntdLocale(currentLocale.value));

const appTheme = {
  token: {
    colorPrimary: "#0089ff", // 钉钉经典蓝
    borderRadius: 6, // 略微圆润的边角
    colorBgLayout: "#f4f5f7", // 浅灰背景色
    colorTextBase: "#1f2329", // 柔和深色字体
    colorBorder: "#e8ebf0", // 清爽边框色
  },
  components: {
    Layout: {
      bodyBg: "#f4f5f7",
      headerBg: "#ffffff",
    },
    Menu: {
      itemBg: "transparent",
      itemActiveBg: "#e6f2ff",
      itemSelectedBg: "#e6f2ff",
      itemSelectedColor: "#0089ff",
      itemHoverBg: "#f2f3f5",
    },
  },
};

watch(
  () => [route.fullPath, currentLocale.value],
  () => {
    applyDocumentTitle(route);
  },
  { immediate: true }
);
</script>
