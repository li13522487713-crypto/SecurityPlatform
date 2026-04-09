<template>
  <a-modal
    :open="open"
    :title="title"
    :confirm-loading="loading"
    :ok-text="confirmText"
    :cancel-text="cancelText"
    @cancel="emit('cancel')"
    @ok="emit('confirm')"
  >
    <slot>
      <p>{{ content }}</p>
    </slot>
  </a-modal>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    open: boolean;
    loading?: boolean;
    title?: string;
    content?: string;
    cancelText?: string;
    confirmText?: string;
  }>(),
  {
    loading: false,
    title: "回滚确认",
    content: "确定回滚到指定版本吗？",
    cancelText: "取消",
    confirmText: "确认回滚"
  }
);

void props;

const emit = defineEmits<{
  cancel: [];
  confirm: [];
}>();
</script>
