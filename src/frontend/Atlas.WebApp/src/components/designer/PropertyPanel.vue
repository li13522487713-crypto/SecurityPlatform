<template>
  <div class="designer-panel">
    <a-tabs v-model:active-key="activeKey">
      <a-tab-pane key="basic" :tab="t('designerUi.tabBasic')">
        <slot name="basic" />
      </a-tab-pane>
      <a-tab-pane key="node" :tab="t('designerUi.tabNode')">
        <slot name="node" />
      </a-tab-pane>
      <a-tab-pane key="validate" :tab="t('designerUi.tabValidate')">
        <slot name="validate" />
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const activeKey = ref("basic");
</script>

<style scoped>
.designer-panel {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  background: #fff;
  padding: 12px;
  min-height: 480px;
}
</style>
