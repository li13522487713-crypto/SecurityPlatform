<template>
  <PageContainer :title="t('console.runtimePreview.title')">
    <a-alert type="info" show-icon :message="t('console.runtimePreview.description')" />
    <a-card>
      <pre class="preview-content">{{ previewInfo }}</pre>
    </a-card>
  </PageContainer>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { PageContainer } from "@atlas/shared-ui";
import { createInitialReleaseState } from "@atlas/runtime-core";

const { t } = useI18n();
const previewInfo = computed(() => JSON.stringify(createInitialReleaseState(), null, 2));
</script>

<style scoped>
.preview-content {
  margin: 0;
  white-space: pre-wrap;
}
</style>
