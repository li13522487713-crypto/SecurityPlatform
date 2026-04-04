<template>
  <a-layout class="app-host-layout">
    <a-layout-header class="app-host-header">
      <div class="app-host-header__left">
        <a-button type="link" @click="goLogin">{{ appKey }}</a-button>
        <span class="app-host-title">{{ activeTitle }}</span>
      </div>
      <LocaleSwitch />
    </a-layout-header>
    <a-layout has-sider>
      <a-layout-sider v-if="menuItems.length" theme="light" width="220" class="app-host-sider">
        <a-menu mode="inline" :selected-keys="selectedKeys">
          <a-menu-item v-for="item in menuItems" :key="item.pageKey" @click="goRuntime(item.pageKey)">
            {{ item.title }}
          </a-menu-item>
        </a-menu>
      </a-layout-sider>
      <a-layout-content class="app-host-content">
        <RouterView />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";
import { getRuntimeMenu, type RuntimeMenuItem } from "@/services/runtime/runtime-api-core";

const route = useRoute();
const router = useRouter();
const menuItems = ref<RuntimeMenuItem[]>([]);

const appKey = computed(() => String(route.params.appKey ?? ""));
const pageKey = computed(() => String(route.params.pageKey ?? ""));
const selectedKeys = computed(() => (pageKey.value ? [pageKey.value] : []));
const activeTitle = computed(() => {
  const matched = menuItems.value.find((item) => item.pageKey === pageKey.value);
  return matched?.title ?? "Runtime";
});

async function loadMenu() {
  if (!appKey.value) {
    menuItems.value = [];
    return;
  }

  const response = await getRuntimeMenu(appKey.value);
  menuItems.value = response.items ?? [];
}

function goRuntime(targetPageKey: string) {
  void router.push(`/app-host/${encodeURIComponent(appKey.value)}/r/${encodeURIComponent(targetPageKey)}`);
}

function goLogin() {
  void router.push(`/app-host/${encodeURIComponent(appKey.value)}/login`);
}

watch(() => appKey.value, () => {
  void loadMenu();
}, { immediate: true });

onMounted(() => {
  void loadMenu();
});
</script>

<style scoped>
.app-host-layout {
  min-height: 100vh;
}

.app-host-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
  padding: 0 16px;
}

.app-host-header__left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.app-host-title {
  font-size: 16px;
  font-weight: 600;
}

.app-host-sider {
  border-right: 1px solid var(--color-border);
}

.app-host-content {
  min-height: calc(100vh - var(--header-height));
  background: var(--color-bg-layout);
  padding: 16px;
}
</style>
