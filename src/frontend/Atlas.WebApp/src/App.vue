<template>
  <router-view v-if="isAuthPage" />
  <ConsoleLayout v-else-if="isConsoleRoute" />
  <AppWorkspaceLayout v-else-if="isAppWorkspaceRoute" />
  <RuntimeLayout v-else-if="isRuntimeRoute" />
  <MainLayout v-else />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import MainLayout from "@/layouts/MainLayout.vue";
import ConsoleLayout from "@/layouts/ConsoleLayout.vue";
import AppWorkspaceLayout from "@/layouts/AppWorkspaceLayout.vue";
import RuntimeLayout from "@/layouts/RuntimeLayout.vue";

const route = useRoute();

const isAuthPage = computed(() => route.path === "/login" || route.path === "/register");
const isConsoleRoute = computed(() => route.path === "/console" || route.path.startsWith("/console/"));
const isAppWorkspaceRoute = computed(() => route.path.startsWith("/apps/"));
const isRuntimeRoute = computed(() => route.path.startsWith("/r/"));
</script>