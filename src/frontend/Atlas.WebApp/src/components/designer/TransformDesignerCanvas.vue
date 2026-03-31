<template>
  <div class="transform-designer-canvas">
    <a-alert type="info" show-icon :message="t('dynamicDesigner.transformHint')" />
    <a-card size="small" :title="t('dynamicDesigner.transformDesign')" style="margin-top: 8px">
      <a-empty :description="t('dynamicDesigner.transformPlaceholder')" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();
</script>

<style scoped>
.transform-designer-canvas {
  min-height: 240px;
}
</style>
