<template>
  <a-layout class="app-runtime-layout">
    <a-layout-sider :width="220" theme="light" collapsible v-model:collapsed="collapsed">
      <div class="logo-section">
        <h3 v-if="!collapsed">{{ appKey }}</h3>
      </div>
      <a-menu mode="inline" :selectedKeys="selectedKeys" @click="handleMenuClick">
        <a-menu-item v-for="item in menuItems" :key="item.pageKey">
          {{ item.title }}
        </a-menu-item>
      </a-menu>
    </a-layout-sider>
    <a-layout>
      <a-layout-header class="runtime-header">
        <a-space>
          <span>{{ t("layout.appRuntime") }}</span>
        </a-space>
        <a-button type="link" @click="handleLogout">{{ t("auth.logout") }}</a-button>
      </a-layout-header>
      <a-layout-content class="runtime-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { useAppUserStore } from "@/stores/user";
import { getRuntimeMenu } from "@/services/api-runtime";
import type { RuntimeMenuItem } from "@/types/api";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();

const collapsed = ref(false);
const menuItems = ref<RuntimeMenuItem[]>([]);
const appKey = computed(() => String(route.params.appKey ?? ""));
const selectedKeys = computed(() => {
  const pageKey = String(route.params.pageKey ?? "");
  return pageKey ? [pageKey] : [];
});

async function loadMenu() {
  if (!appKey.value) return;
  try {
    const menu = await getRuntimeMenu(appKey.value);
    menuItems.value = menu.items;
  } catch {
    menuItems.value = [];
  }
}

function handleMenuClick(info: { key: string | number }) {
  const key = String(info.key);
  void router.push(`/apps/${encodeURIComponent(appKey.value)}/r/${encodeURIComponent(key)}`);
}

async function handleLogout() {
  await userStore.logout();
  void router.push({ name: "app-login", params: { appKey: appKey.value } });
}

onMounted(() => {
  void loadMenu();
});
</script>

<style scoped>
.app-runtime-layout {
  min-height: 100vh;
}
.logo-section {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid #f0f0f0;
}
.logo-section h3 {
  margin: 0;
  font-size: 16px;
  white-space: nowrap;
  overflow: hidden;
}
.runtime-header {
  background: #fff;
  padding: 0 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #f0f0f0;
  height: 48px;
  line-height: 48px;
}
.runtime-content {
  padding: 16px;
  min-height: 280px;
}
</style>
