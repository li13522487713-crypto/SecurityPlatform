<template>
  <div class="table-entity-node">
    <div class="node-header">
      <TableOutlined class="node-icon" />
      <span class="node-name" :title="table.tableKey">{{ table.displayName || table.tableKey }}</span>
      <a-button
        type="text"
        size="small"
        danger
        class="remove-btn"
        @click="emit('remove', id)"
      >
        <template #icon><CloseOutlined /></template>
      </a-button>
    </div>
    <div class="node-body">
      <div v-if="loading" class="node-loading">
        <a-spin size="small" />
      </div>
      <div
        v-for="field in fields"
        :key="field.name"
        class="field-row"
      >
        <Handle :id="`in-${field.name}`" type="target" :position="Position.Left" class="field-handle field-handle-in" />
        <KeyOutlined v-if="field.isPrimaryKey" class="pk-icon" />
        <span v-else class="field-dot" />
        <span class="field-name">{{ field.displayName || field.name }}</span>
        <span class="field-type">{{ field.fieldType }}</span>
        <Handle :id="`out-${field.name}`" type="source" :position="Position.Right" class="field-handle field-handle-out" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { Handle, Position } from "@vue-flow/core";
import { TableOutlined, KeyOutlined, CloseOutlined } from "@ant-design/icons-vue";
import type { DynamicFieldDefinition } from "@/types/dynamic-tables";
import { getDynamicTableFields } from "@/services/dynamic-tables";

const props = defineProps<{
  id: string;
  data: { table: { tableKey: string; displayName: string } };
}>();

const emit = defineEmits<{
  (e: "remove", id: string): void;
}>();

const table = props.data.table;
const fields = ref<DynamicFieldDefinition[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    fields.value = await getDynamicTableFields(table.tableKey);
  } catch {
    // 加载失败不阻断画布
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.table-entity-node {
  min-width: 240px;
  max-width: 280px;
  background: #fff;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 7px 10px;
  background: #f0f5ff;
  border-bottom: 1px solid #d9d9d9;
}

.node-icon {
  color: #1890ff;
  flex-shrink: 0;
}

.node-name {
  flex: 1;
  font-weight: 600;
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.remove-btn {
  flex-shrink: 0;
  opacity: 0.5;
}

.remove-btn:hover {
  opacity: 1;
}

.node-body {
  max-height: 220px;
  overflow-y: auto;
}

.node-loading {
  padding: 10px;
  text-align: center;
}

.field-row {
  position: relative;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 16px;
  font-size: 12px;
  border-bottom: 1px solid #f5f5f5;
}

.field-row:last-child {
  border-bottom: none;
}

.pk-icon {
  color: #faad14;
  font-size: 11px;
  flex-shrink: 0;
}

.field-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #d9d9d9;
  flex-shrink: 0;
}

.field-name {
  flex: 1;
  color: #444;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.field-type {
  color: #aaa;
  font-size: 11px;
  flex-shrink: 0;
}

.field-handle {
  width: 8px;
  height: 8px;
  background: #1677ff;
  border: 1px solid #fff;
}

.field-handle-in {
  left: -5px;
}

.field-handle-out {
  right: -5px;
}
</style>
