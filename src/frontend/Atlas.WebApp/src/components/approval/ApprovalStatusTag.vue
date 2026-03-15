<template>
  <a-tag :color="color">{{ text }}</a-tag>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { ApprovalTaskStatus } from '@/types/api';

const props = defineProps<{
  status?: ApprovalTaskStatus | string | number;
}>();

const color = computed(() => {
  switch (props.status) {
    case ApprovalTaskStatus.Pending: return "processing";
    case ApprovalTaskStatus.Approved: return "success";
    case ApprovalTaskStatus.Rejected: return "error";
    case ApprovalTaskStatus.Canceled: return "default";
    default: return "default";
  }
});

const text = computed(() => {
  switch (props.status) {
    case ApprovalTaskStatus.Pending: return "待审批";
    case ApprovalTaskStatus.Approved: return "已同意";
    case ApprovalTaskStatus.Rejected: return "已驳回";
    case ApprovalTaskStatus.Canceled: return "已取消";
    default: return "未知";
  }
});
</script>
