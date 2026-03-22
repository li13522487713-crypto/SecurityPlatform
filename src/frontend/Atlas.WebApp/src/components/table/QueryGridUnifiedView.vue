<template>
  <div class="query-grid-unified-view">
    <div class="query-panel-container" v-if="showQueryPanel">
      <AdvancedQueryPanel
        :model-value="queryConfig"
        @update:model-value="val => $emit('update:queryConfig', val)"
        :fields="fields"
        :title="queryTitle"
        :show-actions="true"
        @search="handleSearch"
        @reset="handleReset"
      />
    </div>
    
    <div class="table-container">
      <div class="toolbar" v-if="$slots.toolbar">
        <slot name="toolbar"></slot>
      </div>
      <ProTable
        :config="tableConfig"
        :data-source="dataSource"
        :loading="loading"
        :pagination="pagination"
        :row-selection="rowSelection"
        :load-tree-data="loadTreeData"
        @update:config="$emit('update:tableConfig', $event)"
        @change="(pag, fil, sor, ext) => $emit('change', pag, fil, sor, ext)"
        @columnResize="(key, width) => $emit('columnResize', key, width)"
        @expand="(expanded, record) => $emit('expand', expanded, record)"
      >
        <!-- 透传所有的 slot -->
        <template v-for="(_, name) in slotsToForward" v-slot:[name]="slotData" :key="name">
          <slot :name="name" v-bind="slotData || {}"></slot>
        </template>
      </ProTable>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, useSlots } from 'vue';
import type { TableProps } from 'ant-design-vue';
import type { TableViewConfig } from '@/types/api';
import type { AdvancedQueryConfig } from '@/types/advanced-query';
import type { DynamicFieldDefinition } from '@/types/dynamic-tables';
import AdvancedQueryPanel from '@/components/advanced-query/advanced-query-panel.vue';
import ProTable from '@/components/table/pro-table.vue';

interface Props {
  tableConfig: TableViewConfig;
  queryConfig: AdvancedQueryConfig;
  fields: DynamicFieldDefinition[];
  dataSource: any[];
  loading?: boolean;
  pagination?: TableProps["pagination"];
  rowSelection?: TableProps["rowSelection"];
  loadTreeData?: (record: any) => Promise<void>;
  showQueryPanel?: boolean;
  queryTitle?: string;
}

const props = withDefaults(defineProps<Props>(), {
  showQueryPanel: true,
  queryTitle: '高级查询'
});

const emit = defineEmits<{
  (e: "update:tableConfig", config: TableViewConfig): void;
  (e: "update:queryConfig", config: AdvancedQueryConfig): void;
  (e: "change", pagination: any, filters: any, sorter: any, extra: any): void;
  (e: "columnResize", key: string, width: number): void;
  (e: "expand", expanded: boolean, record: any): void;
  (e: "search"): void;
  (e: "reset"): void;
}>();

const slots = useSlots();
const slotsToForward = computed(() => {
  return Object.keys(slots).filter(name => name !== 'toolbar' && name !== 'queryPanel');
});

const handleSearch = () => {
  emit('search');
};

const handleReset = () => {
  emit('reset');
};
</script>

<style scoped>
.query-grid-unified-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.query-panel-container {
  padding: 16px;
  background: #fff;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
}
.table-container {
  background: #fff;
  padding: 16px;
  border-radius: 4px;
}
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
}
</style>
