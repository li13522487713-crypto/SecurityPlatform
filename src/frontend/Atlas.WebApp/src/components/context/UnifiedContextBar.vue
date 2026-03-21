<template>
  <a-space :size="6" class="context-bar" data-testid="e2e-unified-context-bar">
    <a-tag color="geekblue" data-testid="e2e-unified-context-tenant">
      {{ t("contextBar.tenantLabel") }}: {{ tenantLabel }}
    </a-tag>
    <a-tag v-if="showApp && appIdLabel" color="cyan" data-testid="e2e-unified-context-app">
      {{ t("contextBar.appLabel") }}: {{ appIdLabel }}
    </a-tag>
    <ProjectSwitcher class="project-switcher" />
  </a-space>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";

const { t } = useI18n();
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";
import { getTenantId } from "@/utils/auth";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

const props = withDefaults(
  defineProps<{
    showApp?: boolean;
  }>(),
  {
    showApp: true
  }
);

const route = useRoute();

const tenantLabel = computed(() => {
  const tenantId = getTenantId();
  if (!tenantId) {
    return t("contextBar.notAvailable");
  }

  return tenantId;
});

const appIdLabel = computed(() => {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId : "";
  if (routeAppId) {
    return routeAppId;
  }

  return getCurrentAppIdFromStorage() || "";
});
</script>

<style scoped>
.context-bar {
  display: flex;
  align-items: center;
}

.project-switcher {
  margin-left: 2px;
}
</style>
