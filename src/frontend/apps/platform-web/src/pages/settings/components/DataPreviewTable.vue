<template>
  <a-table
    :columns="tableColumns"
    :data-source="data"
    :loading="loading"
    :scroll="{ x: 'max-content', y: 400 }"
    size="small"
    bordered
    :pagination="paginationConfig"
    :row-key="(_: unknown, index: number) => index"
  >
    <template #headerCell="{ column }">
      <span :title="`${column.title} (${column.colType})`">
        {{ column.title }}
        <span class="col-type-hint">{{ column.colType }}</span>
      </span>
    </template>
  </a-table>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { SqlQueryResultColumn } from "@atlas/shared-core";

const props = defineProps<{
  columns: SqlQueryResultColumn[];
  data: Record<string, unknown>[];
  loading: boolean;
}>();

const tableColumns = computed(() =>
  props.columns.map((column) => ({
    title: column.title,
    dataIndex: column.field,
    key: column.field,
    colType: column.type,
    ellipsis: true,
    width: 150
  }))
);

const paginationConfig = computed(() =>
  props.data.length > 50
    ? {
        pageSize: 50,
        showSizeChanger: true,
        pageSizeOptions: ["50", "100", "200", "500"],
        showTotal: (total: number) => `${total}`
      }
    : false
);
</script>

<style scoped>
.col-type-hint {
  font-size: 11px;
  color: #999;
  font-weight: normal;
  margin-left: 4px;
}
</style>
