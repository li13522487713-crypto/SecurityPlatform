<template>
  <a-space :size="6" class="context-bar">
    <a-tag color="geekblue">
      {{ t("contextBar.tenantLabel") }}: {{ tenantLabel }}
    </a-tag>
  </a-space>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { getTenantId } from "@atlas/shared-core";

const { t } = useI18n();

const tenantLabel = computed(() => {
  const tenantId = getTenantId();
  if (!tenantId) return t("contextBar.notAvailable");
  return tenantId;
});
</script>

<style scoped>
.context-bar {
  display: flex;
  align-items: center;
}
</style>
