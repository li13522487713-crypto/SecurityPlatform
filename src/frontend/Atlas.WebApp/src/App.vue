<template>
  <a-config-provider :locale="antdLocale" :theme="appTheme">
    <router-view v-if="isAuthPage" />
    <ConsoleLayout v-else-if="isConsoleRoute" />
    <AppWorkspaceLayout v-else-if="isAppWorkspaceRoute" />
    <RuntimeLayout v-else-if="isRuntimeRoute" />
    <MainLayout v-else />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import MainLayout from "@/layouts/MainLayout.vue";
import ConsoleLayout from "@/layouts/ConsoleLayout.vue";
import AppWorkspaceLayout from "@/layouts/AppWorkspaceLayout.vue";
import RuntimeLayout from "@/layouts/RuntimeLayout.vue";
import { getActiveLocale, getAntdLocale } from "@/i18n";
import { applyDocumentTitle } from "@/utils/i18n-navigation";

const route = useRoute();

const isAuthPage = computed(() => route.path === "/login" || route.path === "/register");
const isConsoleRoute = computed(() => route.path === "/console" || route.path.startsWith("/console/"));
const isAppWorkspaceRoute = computed(() => route.path.startsWith("/apps/"));
const isRuntimeRoute = computed(() => route.path.startsWith("/r/"));
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
