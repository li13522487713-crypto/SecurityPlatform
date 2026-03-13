<template>
  <a-card :title="title" class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keywordModel"
          :placeholder="searchPlaceholder"
          allow-clear
          @press-enter="$emit('search')"
        />
        <a-button @click="$emit('search')">查询</a-button>
        <a-button @click="$emit('reset')">重置</a-button>
        <slot name="toolbar-actions" />
      </a-space>
      <a-space wrap>
        <slot name="toolbar-right" />
      </a-space>
    </div>

    <div v-if="$slots.filter" class="crud-filter-bar">
      <a-space wrap>
        <span class="crud-filter-label">高级筛选</span>
        <slot name="filter" />
      </a-space>
    </div>

    <slot name="table" />

    <a-drawer
      v-model:open="drawerOpenModel"
      :title="drawerTitle"
      placement="right"
      :width="drawerWidth"
      @close="$emit('close-form')"
      destroy-on-close
    >
      <slot name="form" />
      <template #footer>
        <a-space>
          <a-button :disabled="submitDisabled || submitLoading" @click="$emit('close-form')">取消</a-button>
          <a-button
            type="primary"
            :loading="submitLoading"
            :disabled="submitDisabled"
            @click="$emit('submit')"
          >
            保存
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <slot name="extra-drawers" />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  title: string;
  keyword: string;
  searchPlaceholder?: string;
  drawerOpen: boolean;
  drawerTitle: string;
  drawerWidth?: number | string;
  submitLoading?: boolean;
  submitDisabled?: boolean;
}>();

const submitLoading = computed(() => props.submitLoading ?? false);
const submitDisabled = computed(() => props.submitDisabled ?? false);

const emit = defineEmits<{
  (e: "update:keyword", value: string): void;
  (e: "update:drawerOpen", value: boolean): void;
  (e: "search"): void;
  (e: "reset"): void;
  (e: "close-form"): void;
  (e: "submit"): void;
}>();

const keywordModel = computed({
  get: () => props.keyword,
  set: (value: string) => emit("update:keyword", value)
});

const drawerOpenModel = computed({
  get: () => props.drawerOpen,
  set: (value: boolean) => emit("update:drawerOpen", value)
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-bar {
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-label {
  color: var(--color-text-tertiary);
}
</style>
