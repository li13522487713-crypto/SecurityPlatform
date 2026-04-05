<template>
  <div class="preview-wrap">
    <a-alert
      type="info"
      show-icon
      :message="t('approvalDesigner.previewHint')"
      class="preview-alert"
    />
    <a-card size="small" :title="t('approvalDesigner.previewTreeTitle')" class="preview-card">
      <pre class="preview-json">{{ treeJson }}</pre>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import type { ApprovalFlowTree } from '@/types/approval-tree';

const props = defineProps<{
  flowTree: ApprovalFlowTree;
}>();

const { t } = useI18n();

const treeJson = computed(() => JSON.stringify(props.flowTree, null, 2));
</script>

<style scoped>
.preview-wrap {
  width: 100%;
  height: 100%;
  padding: 12px;
  background: #f7f8fa;
  overflow: auto;
}

.preview-alert {
  margin-bottom: 12px;
}

.preview-card {
  min-height: calc(80vh - 120px);
}

.preview-json {
  margin: 0;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
