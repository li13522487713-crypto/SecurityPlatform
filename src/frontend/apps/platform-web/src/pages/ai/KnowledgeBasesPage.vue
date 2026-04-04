<template>
  <div style="padding: 24px;">
    <a-card>
      <a-result status="info" :title="t('route.knowledgeBases')" sub-title="Page migration pending..." />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
const { t } = useI18n();
</script>
