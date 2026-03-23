<template>
  <div class="entity-node" :class="{ 'is-selected': isSelected }">
    <div class="node-header">
      <div class="node-title" :title="table.tableKey">
        <TableOutlined style="margin-right: 6px"/>
        {{ table.displayName || table.tableKey }}
      </div>
    </div>
    <div class="node-body">
      <div v-for="field in fields" :key="field.name" class="field-item">
        <div class="field-icon">
          <KeyOutlined v-if="field.isPrimaryKey" class="pk-icon" title="主键" />
          <span v-else class="normal-icon"></span>
        </div>
        <div class="field-name" :title="field.displayName || undefined">{{ field.name }}</div>
        <div class="field-type">{{ field.fieldType }}</div>
      </div>
      <div v-if="loading" class="loading-fields">
        <a-spin size="small" /> 加载中...
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, onMounted } from 'vue';
import { TableOutlined, KeyOutlined } from '@ant-design/icons-vue';
import type { DynamicTableListItem, DynamicFieldDefinition } from '@/types/dynamic-tables';
import { getDynamicTableFields } from '@/services/dynamic-tables';
import type { Node } from '@antv/x6';

const getNode = inject('getNode') as () => Node;
const node = getNode();

const table = ref<DynamicTableListItem>(node.getData().table);
const isSelected = ref(false);
const fields = ref<DynamicFieldDefinition[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    fields.value = await getDynamicTableFields(table.value.tableKey);
  } catch (error) {
    console.error("加载字段失败", error);
  } finally {
    loading.value = false;
  }
  
  node.on('change:data', ({ current }) => {
    table.value = current.table;
  });
  
  // Note: Selection handling usually requires hooking into graph events
});
</script>

<style scoped>
.entity-node {
  width: 220px;
  background-color: #fff;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  box-shadow: 0 2px 5px rgba(0,0,0,0.05);
  overflow: hidden;
  transition: box-shadow 0.2s, border-color 0.2s;
}

.entity-node.is-selected {
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.2);
}

.node-header {
  background-color: #fafafa;
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
  font-weight: 600;
  color: #262626;
  font-size: 14px;
}

.node-title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-body {
  padding: 4px 0;
  max-height: 250px;
  overflow-y: auto;
}

.field-item {
  display: flex;
  align-items: center;
  padding: 4px 12px;
  font-size: 12px;
  border-bottom: 1px solid #f0f0f0;
}

.field-item:last-child {
  border-bottom: none;
}

.field-icon {
  width: 16px;
  flex-shrink: 0;
}

.pk-icon {
  color: #faad14;
}

.field-name {
  flex-grow: 1;
  color: #595959;
  margin-left: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.field-type {
  color: #999;
  font-size: 11px;
  flex-shrink: 0;
}

.loading-fields {
  text-align: center;
  padding: 12px;
  color: #999;
  font-size: 12px;
}
</style>
