<template>
  <a-modal
    :open="open"
    :title="result?.isValid ? '校验通过' : '校验结果'"
    :footer="null"
    width="520px"
    @update:open="emit('update:open', $event)"
  >
    <div v-if="result">
      <a-alert
        v-if="result.isValid"
        type="success"
        message="流程校验通过，可以发布"
        show-icon
        style="margin-bottom:12px"
      />
      <template v-else>
        <a-alert type="error" message="校验不通过，请修正以下问题" show-icon style="margin-bottom:12px" />
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
            <a-tag v-if="issue.nodeId" color="processing" style="margin-left: 8px">点击定位</a-tag>
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
          <a-tag v-if="issue.nodeId" color="gold" style="margin-left: 8px">点击定位</a-tag>
        </div>
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { CloseCircleOutlined, ExclamationCircleOutlined } from '@ant-design/icons-vue';
import type { ApprovalFlowValidationIssue, ApprovalFlowValidationResult } from '@/types/api';

defineProps<{
  open: boolean;
  result: ApprovalFlowValidationResult | null;
  errorIssues: ApprovalFlowValidationIssue[];
  warningIssues: ApprovalFlowValidationIssue[];
}>();

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
