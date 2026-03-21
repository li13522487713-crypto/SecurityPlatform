<template>
  <div class="designer-canvas">
    <slot>
      <a-empty :description="t('designerUi.canvasEmpty')" />
    </slot>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();
</script>

<style scoped>
.designer-canvas {
  border: 1px dashed #d9d9d9;
  border-radius: 6px;
  padding: 12px;
  background: #fff;
  min-height: 480px;
}
</style>
