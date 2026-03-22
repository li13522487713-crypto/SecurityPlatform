<script setup lang="ts">
import { computed, useSlots, ref } from "vue";
import type { TableProps } from "ant-design-vue";
import type { TableViewConfig, TableViewColumnConfig } from "@/types/api";

interface Props {
  config: TableViewConfig;
  dataSource: any[];
  loading?: boolean;
  pagination?: TableProps["pagination"];
  rowSelection?: TableProps["rowSelection"];
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "update:config", config: TableViewConfig): void;
  (e: "change", pagination: any, filters: any, sorter: any, extra: any): void;
  (e: "columnResize", key: string, width: number): void;
}>();

const slots = useSlots();

// 辅助方法：深拷贝列配置以便转换给 Ant Design Vue
const mapColumnsToAntd = (columns: TableViewColumnConfig[]): TableProps["columns"] => {
  if (!columns) return [];
  return columns.map(col => {
    const antdCol: any = {
      ...col,
      // Ant Design Vue 中 fixed: false 会引发类型问题或错误渲染，使用 undefined
      fixed: col.pinned === false ? undefined : col.pinned,
      children: col.children && col.children.length > 0 ? mapColumnsToAntd(col.children) : undefined,
    };
    if (col.colSpan !== undefined) antdCol.colSpan = col.colSpan;
    if (col.rowSpan !== undefined) antdCol.rowSpan = col.rowSpan;
    
    // 如果该列允许 resizable，添加自定义 headerCell 控制 class
    if (col.resizable) {
      antdCol.customHeaderCell = () => {
        return {
          class: 'resizable-header-wrapper',
        };
      };
    }

    return antdCol;
  });
};

const antTableProps = computed(() => ({
  columns: mapColumnsToAntd(props.config.columns || []),
  scroll: props.config.scroll,
  bordered: props.config.bordered,
  size: props.config.density === "compact" ? "small" : props.config.density === "comfortable" ? "default" : "middle",
}));

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
    >
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
</style>
