<template>
  <a-drawer :open="open" :title="title" :width="520" @close="emit('close')">
    <slot></slot>
    <template #footer>
      <a-space>
        <a-button @click="emit('close')">{{ cancelText }}</a-button>
        <a-button type="primary" :loading="loading" @click="emit('confirm')">{{ confirmText }}</a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    open: boolean;
    title?: string;
    loading?: boolean;
    cancelText?: string;
    confirmText?: string;
  }>(),
  {
    title: "发布",
    loading: false,
    cancelText: "取消",
    confirmText: "确认发布"
  }
);

void props;

const emit = defineEmits<{
  close: [];
  confirm: [];
}>();
</script>
