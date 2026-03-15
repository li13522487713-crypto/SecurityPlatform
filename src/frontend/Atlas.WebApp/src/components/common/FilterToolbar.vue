<template>
  <div class="filter-toolbar">
    <div v-if="$slots.title || title" class="toolbar-left">
      <slot name="title">
        <h2 v-if="title" class="toolbar-title">{{ title }}</h2>
      </slot>
    </div>
    
    <div class="toolbar-right">
      <a-space>
        <!-- 默认插槽：允许插入自定义的筛选组件 -->
        <slot></slot>

        <!-- 标准搜索框 -->
        <a-input-search
          v-if="showSearch"
          :value="keyword"
          @update:value="(v: string) => $emit('update:keyword', v)"
          allow-clear
          :placeholder="searchPlaceholder"
          :style="{ width: searchWidth + 'px' }"
          @search="handleSearch"
        />

        <!-- 刷新按钮 -->
        <a-button v-if="showRefresh" @click="handleRefresh">刷新</a-button>

        <!-- 自定义操作插槽 (如新增按钮、导出等) -->
        <slot name="actions"></slot>
      </a-space>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title?: string;
  keyword?: string;
  showSearch?: boolean;
  searchPlaceholder?: string;
  searchWidth?: number;
  showRefresh?: boolean;
}>();

const emit = defineEmits<{
  'update:keyword': [value: string];
  'search': [value: string];
  'refresh': [];
}>();

const handleSearch = (val: string) => {
  emit('search', val);
};

const handleRefresh = () => {
  emit('refresh');
};
</script>

<style scoped>
.filter-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
}

/* 如果只传入 right 内容，确保靠右对齐 */
.toolbar-left:empty + .toolbar-right,
.filter-toolbar > .toolbar-right:first-child {
  margin-left: auto;
}

.toolbar-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
}
</style>
