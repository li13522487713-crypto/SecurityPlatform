<template>
  <a-modal
    :open="open"
    :title="modalTitle"
    :footer="null"
    width="520px"
    @update:open="emit('update:open', $event)"
  >
    <div v-if="result">
      <a-alert
        v-if="result.isValid"
        type="success"
        :message="t('approvalDesigner.valPassMsg')"
        show-icon
        style="margin-bottom:12px"
      />
      <template v-else>
        <a-alert type="error" :message="t('approvalDesigner.valFailMsg')" show-icon style="margin-bottom:12px" />
        <div class="dd-validate-list">
          <div
            v-for="(issue, idx) in errorIssues"
            :key="`${issue.code}-${idx}`"
            class="dd-validate-item"
            :class="{ 'dd-validate-item--locatable': !!issue.nodeId }"
            @click="emit('locate', issue)"
          >
            <CloseCircleOutlined style="color:#ff4d4f;margin-right:6px" />
            <span>{{ issue.message }}</span>
            <a-tag v-if="issue.nodeId" color="processing" style="margin-left: 8px">{{ t('approvalDesigner.valTagLocate') }}</a-tag>
          </div>
        </div>
      </template>
      <div v-if="warningIssues.length" class="dd-validate-list" style="margin-top:8px">
        <div
          v-for="(issue, idx) in warningIssues"
          :key="`${issue.code}-${idx}`"
          class="dd-validate-item"
          :class="{ 'dd-validate-item--locatable': !!issue.nodeId }"
          @click="emit('locate', issue)"
        >
          <ExclamationCircleOutlined style="color:#faad14;margin-right:6px" />
          <span>{{ issue.message }}</span>
          <a-tag v-if="issue.nodeId" color="gold" style="margin-left: 8px">{{ t('approvalDesigner.valTagLocate') }}</a-tag>
        </div>
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { CloseCircleOutlined, ExclamationCircleOutlined } from '@ant-design/icons-vue';
import type { ApprovalFlowValidationIssue, ApprovalFlowValidationResult } from '@/types/api';

const { t } = useI18n();

const props = defineProps<{
  open: boolean;
  result: ApprovalFlowValidationResult | null;
  errorIssues: ApprovalFlowValidationIssue[];
  warningIssues: ApprovalFlowValidationIssue[];
}>();

const modalTitle = computed(() =>
  props.result?.isValid ? t('approvalDesigner.valPanelTitleOk') : t('approvalDesigner.valPanelTitleResult'),
);

const emit = defineEmits<{
  'update:open': [value: boolean];
  locate: [issue: ApprovalFlowValidationIssue];
}>();
</script>

<style scoped>
.dd-validate-list {
  max-height: 280px;
  overflow-y: auto;
}
.dd-validate-item {
  padding: 4px 0;
  font-size: 13px;
  line-height: 1.5;
  border-bottom: 1px solid #fafafa;
  display: flex;
  align-items: center;
}
.dd-validate-item:last-child {
  border-bottom: none;
}
.dd-validate-item--locatable {
  cursor: pointer;
  transition: background 0.2s;
}
.dd-validate-item--locatable:hover {
  background: #f5f5f5;
}
</style>
