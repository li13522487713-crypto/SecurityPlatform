<template>
  <div class="search-filter-bar">
    <a-input-search
      :value="keyword"
      :placeholder="placeholder"
      allow-clear
      @update:value="onKeywordChange"
      @search="onSearch"
    />
    <slot></slot>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    keyword?: string;
    placeholder?: string;
  }>(),
  {
    keyword: "",
    placeholder: "请输入关键字搜索"
  }
);

const emit = defineEmits<{
  "update:keyword": [value: string];
  search: [value: string];
}>();

function onKeywordChange(value: string) {
  emit("update:keyword", value);
}

function onSearch(value: string) {
  emit("search", value);
}
</script>

<style scoped>
.search-filter-bar {
  display: flex;
  gap: 8px;
  align-items: center;
}
</style>
