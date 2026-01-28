<template>
  <router-view v-if="isLogin" />
  <a-layout v-else class="app-shell">
    <a-layout-sider collapsible :collapsed="collapsed" @collapse="toggle">
      <div class="brand">Atlas 安全平台</div>
      <a-menu theme="dark" mode="inline" :selected-keys="selectedKeys">
        <a-menu-item key="home" @click="go('/')">总览</a-menu-item>
        <a-menu-item key="assets" @click="go('/assets')">资产</a-menu-item>
        <a-menu-item key="audit" @click="go('/audit')">审计</a-menu-item>
        <a-menu-item key="alert" @click="go('/alert')">告警</a-menu-item>
        <a-menu-item key="approval" @click="go('/approval/flows')">审批流</a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="app-header">
        <div class="header-left">多租户安全支撑平台</div>
        <div class="header-right">
          <a-button type="text" @click="logout">退出</a-button>
        </div>
      </a-layout-header>
      <a-layout-content class="app-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";

const collapsed = ref(false);
const router = useRouter();
const route = useRoute();

const isLogin = computed(() => route.name === "login");

const selectedKeys = computed(() => {
  if (route.path.startsWith("/assets")) return ["assets"];
  if (route.path.startsWith("/audit")) return ["audit"];
  if (route.path.startsWith("/alert")) return ["alert"];
  if (route.path.startsWith("/approval")) return ["approval"];
  return ["home"];
});

const toggle = (value: boolean) => {
  collapsed.value = value;
};

const go = (path: string) => {
  router.push(path);
};

const logout = () => {
  localStorage.removeItem("access_token");
  router.push("/login");
};
</script>