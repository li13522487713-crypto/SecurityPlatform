<script setup lang="ts">
import { computed, useSlots, ref, h } from "vue";
import { LoadingOutlined } from "@ant-design/icons-vue";
import type { TableProps } from "ant-design-vue";
import type { TableViewConfig, TableViewColumnConfig } from "@/types/api";
import VScrollBody from "./VScrollBody.vue";

interface Props {
  config: TableViewConfig;
  dataSource: any[];
  loading?: boolean;
  pagination?: TableProps["pagination"];
  rowSelection?: TableProps["rowSelection"];
  loadTreeData?: (record: any) => Promise<void>;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "update:config", config: TableViewConfig): void;
  (e: "change", pagination: any, filters: any, sorter: any, extra: any): void;
  (e: "columnResize", key: string, width: number): void;
  (e: "expand", expanded: boolean, record: any): void;
}>();

const slots = useSlots();

// 辅助方法：深拷贝列配置以便转换给 Ant Design Vue
const mapColumnsToAntd = (columns: TableViewColumnConfig[]): TableProps["columns"] => {
  if (!columns) return [];
  return columns.map(col => {
    const customCell = (record: any) => {
      const style: any = {};
      
      // 动态评估 Excel 式条件格式
      if (col.conditionalFormats && col.conditionalFormats.length > 0) {
         const cellValue = record[col.dataIndex || col.key];
         for (const format of col.conditionalFormats) {
           let matched = false;
           if (cellValue != null && cellValue !== '') {
             const val = Number(cellValue);
             const threshold = Number(format.value);
             switch (format.operator) {
               case 'equal': matched = cellValue == format.value; break;
               case 'notEqual': matched = cellValue != format.value; break;
               case 'greaterThan': if(!isNaN(val) && !isNaN(threshold)) matched = val > threshold; break;
               case 'lessThan': if(!isNaN(val) && !isNaN(threshold)) matched = val < threshold; break;
               case 'between': 
                 const t2 = Number(format.value2);
                 if(!isNaN(val) && !isNaN(threshold) && !isNaN(t2)) {
                   matched = val >= threshold && val <= t2; 
                 }
                 break;
               case 'contains': 
                 matched = String(cellValue).toLowerCase().includes(String(format.value).toLowerCase()); 
                 break;
             }
           }
           if (matched) {
             if (format.color) style.color = format.color;
             if (format.backgroundColor) style.backgroundColor = format.backgroundColor;
             break; // 第一个匹配的规则生效并阻断
           }
         }
      }
      
      return { style };
    };

    const customHeaderCell = () => {
      return {
        class: 'resizable-header-wrapper',
      };
    };

    const antdCol: any = {
      ...col,
      title: col.title || col.key,
      dataIndex: col.dataIndex || col.key,
      width: col.width,
      minWidth: col.minWidth,
      maxWidth: col.maxWidth,
      fixed: col.pinned === "left" || col.pinned === "right" ? col.pinned : undefined,
      customHeaderCell: col.resizable !== false ? customHeaderCell : undefined,
      customCell: customCell,
      children: col.children && col.children.length > 0 ? mapColumnsToAntd(col.children) : undefined,
    };
    if (col.colSpan !== undefined) antdCol.colSpan = col.colSpan;
    if (col.rowSpan !== undefined) antdCol.rowSpan = col.rowSpan;

    return antdCol;
  });
};

const antTableProps = computed(() => {
  const base: any = {
    columns: mapColumnsToAntd(props.config.columns || []),
    scroll: props.config.scroll,
    bordered: props.config.bordered,
    size: props.config.density === "compact" ? "small" : props.config.density === "comfortable" ? "default" : "middle",
  };

  if (props.config.virtual) {
    base.components = {
      body: {
        wrapper: (wProps: any, { slots }: any) => h(VScrollBody, { ...wProps, itemSize: props.config.itemSize || 54 }, slots)
      }
    };
  }

  return base;
});

// 列宽拖拽相关
let isResizing = false;
let startX = 0;
let startWidth = 0;
let resizingColumnKey: string | null = null;
const tableContainerRef = ref<HTMLElement | null>(null);

const startResize = (key: string, event: MouseEvent, targetWidth: number | string | undefined) => {
  event.stopPropagation();
  isResizing = true;
  resizingColumnKey = key;
  startX = event.clientX;
  
  // 处理初始宽度
  if (typeof targetWidth === "number") {
    startWidth = targetWidth;
  } else if (typeof targetWidth === "string" && targetWidth.endsWith("px")) {
    startWidth = parseInt(targetWidth, 10);
  } else {
    // 降级：DOM获取宽度
    const th = (event.target as HTMLElement).closest('th');
    startWidth = th ? th.offsetWidth : 100;
  }
  
  document.addEventListener("mousemove", doResize);
  document.addEventListener("mouseup", stopResize);
  document.body.style.cursor = "col-resize";
};

const doResize = (event: MouseEvent) => {
  if (!isResizing || !resizingColumnKey) return;
  
  const deltaX = event.clientX - startX;
  let newWidth = Math.max(startWidth + deltaX, 40); // 最小宽度硬性限制 40
  
  // 查找原配置中的边界限制
  const findCol = (cols: TableViewColumnConfig[], k: string): TableViewColumnConfig | null => {
    for (const c of cols) {
      if (c.key === k) return c;
      if (c.children) {
        const found = findCol(c.children, k);
        if (found) return found;
      }
    }
    return null;
  };
  
  const colModel = findCol(props.config.columns || [], resizingColumnKey);
  if (colModel) {
    if (colModel.minWidth && newWidth < colModel.minWidth) newWidth = colModel.minWidth;
    if (colModel.maxWidth && newWidth > colModel.maxWidth) newWidth = colModel.maxWidth;
  }
  
  emit("columnResize", resizingColumnKey, newWidth);
};

const stopResize = () => {
  isResizing = false;
  resizingColumnKey = null;
  document.removeEventListener("mousemove", doResize);
  document.removeEventListener("mouseup", stopResize);
  document.body.style.cursor = "";
};

const loadingKeys = ref<Set<string>>(new Set());
const handleExpand = async (expanded: boolean, record: any) => {
  emit("expand", expanded, record);
  
  if (expanded && props.loadTreeData && (!record.children || record.children.length === 0)) {
    const key = record.id || record.key;
    if (loadingKeys.value.has(key)) return;
    
    loadingKeys.value.add(key);
    try {
      await props.loadTreeData(record);
    } finally {
      loadingKeys.value.delete(key);
    }
  }
};
</script>

<template>
  <div class="pro-table-wrapper" ref="tableContainerRef">
    <a-table
      v-bind="antTableProps"
      :data-source="dataSource"
      :loading="loading"
      :pagination="pagination"
      :row-selection="rowSelection"
      row-key="id"
      @change="(pag: any, filters: any, sorter: any, extra: any) => emit('change', pag, filters, sorter, extra)"
      @expand="handleExpand"
    >
      <!-- 透传高级嵌套子表展开 -->
      <template #expandedRowRender="slotProps" v-if="slots.expandedRowRender">
        <slot name="expandedRowRender" v-bind="slotProps"></slot>
      </template>

      <!-- 透传底部聚合栏 summary -->
      <template #summary="slotProps" v-if="slots.summary">
        <slot name="summary" v-bind="slotProps"></slot>
      </template>

      <!-- 使用原生的 expandIcon 来提供加载态包裹 -->
      <template #expandIcon="{ expanded, onExpand, record }">
        <template v-if="(record.children && record.children.length > 0) || record.hasChildren">
           <span v-if="loadingKeys.has(record.id || record.key)" class="pro-tree-loading">
             <LoadingOutlined />
           </span>
           <div v-else @click="e => onExpand(record, e)" :class="['ant-table-row-expand-icon', expanded ? 'ant-table-row-expand-icon-expanded' : 'ant-table-row-expand-icon-collapsed']"></div>
        </template>
      </template>

      <!-- 透传原生 HeaderCell Slot 并植入 Resize 手柄 -->
      <template #headerCell="{ column }">
        <slot name="headerCell" :column="column">
          <div class="pro-table-header-cell">
            <span class="pro-table-header-title">{{ column.title || column.key }}</span>
            <div 
              v-if="column.resizable"
              class="pro-table-resize-handle"
              @mousedown="startResize(column.key, $event, column.width)"
            ></div>
          </div>
        </slot>
      </template>

      <!-- 动态透传所有 Body 单元格 Slots -->
      <template #bodyCell="slotProps">
        <!-- 若业务配置了 ellipsis 和 tooltip -->
        <template v-if="slotProps.column.ellipsis && slotProps.column.tooltip">
           <a-tooltip :title="slotProps.record[slotProps.column.dataIndex || slotProps.column.key]">
              <div class="ellipsis-text">
                <slot :name="`bodyCell-${slotProps.column.key}`" v-bind="slotProps">
                  <!-- 若未覆盖具体行插槽，优先走原生的 generic bodyCell -->
                  <slot name="bodyCell" v-bind="slotProps">
                    {{ slotProps.record[slotProps.column.dataIndex || slotProps.column.key] }}
                  </slot>
                </slot>
              </div>
           </a-tooltip>
        </template>
        <!-- 普通渲染 -->
        <template v-else>
          <slot :name="`bodyCell-${slotProps.column.key}`" v-bind="slotProps">
            <slot name="bodyCell" v-bind="slotProps">
              {{ slotProps.record[slotProps.column.dataIndex || slotProps.column.key] }}
            </slot>
          </slot>
        </template>
      </template>
      
      <!-- 透传其他杂项槽位（如 expandComponent） -->
      <template v-for="(_, name) in slots" v-slot:[name]="slotData" :key="name">
         <slot :name="name" v-bind="slotData || {}" v-if="name !== 'headerCell' && name !== 'bodyCell' && !String(name).startsWith('bodyCell-')"></slot>
      </template>
    </a-table>
  </div>
</template>

<style scoped>
.pro-table-wrapper {
  position: relative;
  width: 100%;
}

:deep(.resizable-header-wrapper) {
  position: relative;
  padding-right: 0 !important; /* 给 handle 腾出空间并自己控制 padding */
}

.pro-table-header-cell {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
  padding-right: 12px;
}

.pro-table-header-title {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pro-table-resize-handle {
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  width: 10px;
  cursor: col-resize;
  z-index: 1;
}
.pro-table-resize-handle:hover::after {
  content: '';
  position: absolute;
  top: 25%;
  bottom: 25%;
  right: 4px;
  width: 2px;
  background-color: var(--ant-primary-color, #1890ff);
}

.ellipsis-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.pro-tree-loading {
  display: inline-block;
  margin-right: 8px;
  color: var(--ant-primary-color, #1890ff);
  font-size: 14px;
}
</style>
