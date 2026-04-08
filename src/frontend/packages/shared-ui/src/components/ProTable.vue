<script setup lang="ts">
import { computed, useSlots, ref, h } from "vue";
import { LoadingOutlined } from "@ant-design/icons-vue";
import type { TableProps } from "ant-design-vue";
import type { TableViewConfig, TableViewColumnConfig } from "@atlas/shared-core";

interface Props {
  config: TableViewConfig;
  dataSource: Record<string, unknown>[];
  loading?: boolean;
  pagination?: TableProps["pagination"];
  rowSelection?: TableProps["rowSelection"];
  loadTreeData?: (record: Record<string, unknown>) => Promise<void>;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "update:config", config: TableViewConfig): void;
  (e: "change", pagination: unknown, filters: unknown, sorter: unknown, extra: unknown): void;
  (e: "columnResize", key: string, width: number): void;
  (e: "expand", expanded: boolean, record: unknown): void;
}>();

const slots = useSlots();

const DEFAULT_LEAF_COLUMN_WIDTH = 160;

const mapColumnsToAntd = (
  columns: TableViewColumnConfig[]
): TableProps["columns"] => {
  if (!columns) return [];
  return columns.map((col) => {
    const customCell = (record: Record<string, unknown>) => {
      const style: Record<string, string> = {};

      if (col.conditionalFormats && col.conditionalFormats.length > 0) {
        const cellValue = record[col.dataIndex || col.key];
        for (const format of col.conditionalFormats) {
          let matched = false;
          if (cellValue != null && cellValue !== "") {
            const val = Number(cellValue);
            const threshold = Number(format.value);
            switch (format.operator) {
              case "equal":
                matched = cellValue == format.value;
                break;
              case "notEqual":
                matched = cellValue != format.value;
                break;
              case "greaterThan":
                if (!isNaN(val) && !isNaN(threshold))
                  matched = val > threshold;
                break;
              case "lessThan":
                if (!isNaN(val) && !isNaN(threshold))
                  matched = val < threshold;
                break;
              case "between": {
                const t2 = Number(format.value2);
                if (!isNaN(val) && !isNaN(threshold) && !isNaN(t2)) {
                  matched = val >= threshold && val <= t2;
                }
                break;
              }
              case "contains":
                matched = String(cellValue)
                  .toLowerCase()
                  .includes(String(format.value).toLowerCase());
                break;
            }
          }
          if (matched) {
            if (format.color) style.color = format.color;
            if (format.backgroundColor)
              style.backgroundColor = format.backgroundColor;
            break;
          }
        }
      }

      return { style };
    };

    const customHeaderCell = () => {
      return {
        class: "resizable-header-wrapper",
      };
    };

    const isLeaf = !col.children || col.children.length === 0;

    const normalizedWidth =
      typeof col.width === "number"
        ? col.width
        : typeof col.width === "string" && col.width.trim().endsWith("px")
          ? Number.parseInt(col.width, 10)
          : undefined;

    const resizableEnabled = col.resizable !== false;
    const leafWidth =
      typeof normalizedWidth === "number" && Number.isFinite(normalizedWidth)
        ? normalizedWidth
        : DEFAULT_LEAF_COLUMN_WIDTH;

    const antdCol: Record<string, unknown> = {
      key: col.key,
      title: col.title || col.key,
      dataIndex: col.dataIndex || col.key,
      align: col.align,
      ellipsis: col.ellipsis,
      __resizable: resizableEnabled,
      resizable: false,
      fixed:
        col.pinned === "left" || col.pinned === "right"
          ? col.pinned
          : undefined,
      customHeaderCell: resizableEnabled ? customHeaderCell : undefined,
      customCell,
      children:
        col.children && col.children.length > 0
          ? mapColumnsToAntd(col.children)
          : undefined,
    };

    if (isLeaf) {
      antdCol.width = leafWidth;
    } else if (
      typeof normalizedWidth === "number" &&
      Number.isFinite(normalizedWidth)
    ) {
      antdCol.width = normalizedWidth;
    }
    if (typeof col.minWidth === "number" && Number.isFinite(col.minWidth)) {
      antdCol.minWidth = col.minWidth;
    }
    if (typeof col.maxWidth === "number" && Number.isFinite(col.maxWidth)) {
      antdCol.maxWidth = col.maxWidth;
    }
    if (col.colSpan !== undefined) antdCol.colSpan = col.colSpan;
    if (col.rowSpan !== undefined) antdCol.rowSpan = col.rowSpan;

    return antdCol;
  });
};

const antTableProps = computed(() => {
  const base: Record<string, unknown> = {
    columns: mapColumnsToAntd(props.config.columns || []),
    scroll: props.config.scroll,
    bordered: props.config.bordered,
    size:
      props.config.density === "compact"
        ? "small"
        : props.config.density === "comfortable"
          ? "default"
          : "middle",
  };

  return base;
});

let isResizing = false;
let startX = 0;
let startWidth = 0;
let resizingColumnKey: string | null = null;
const tableContainerRef = ref<HTMLElement | null>(null);

const startResize = (
  key: string,
  event: MouseEvent,
  targetWidth: number | string | undefined
) => {
  event.stopPropagation();
  isResizing = true;
  resizingColumnKey = key;
  startX = event.clientX;

  if (typeof targetWidth === "number") {
    startWidth = targetWidth;
  } else if (typeof targetWidth === "string" && targetWidth.endsWith("px")) {
    startWidth = parseInt(targetWidth, 10);
  } else {
    const th = (event.target as HTMLElement).closest("th");
    startWidth = th ? th.offsetWidth : 100;
  }

  document.addEventListener("mousemove", doResize);
  document.addEventListener("mouseup", stopResize);
  document.body.style.cursor = "col-resize";
};

const doResize = (event: MouseEvent) => {
  if (!isResizing || !resizingColumnKey) return;

  const deltaX = event.clientX - startX;
  let newWidth = Math.max(startWidth + deltaX, 40);

  const findCol = (
    cols: TableViewColumnConfig[],
    k: string
  ): TableViewColumnConfig | null => {
    for (const c of cols) {
      if (c.key === k) return c;
      if (c.children) {
        const found = findCol(c.children, k);
        if (found) return found;
      }
    }
    return null;
  };

  const colModel = findCol(
    props.config.columns || [],
    resizingColumnKey
  );
  if (colModel) {
    if (colModel.minWidth && newWidth < colModel.minWidth)
      newWidth = colModel.minWidth;
    if (colModel.maxWidth && newWidth > colModel.maxWidth)
      newWidth = colModel.maxWidth;
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
const handleExpand = async (expanded: boolean, record: Record<string, unknown>) => {
  emit("expand", expanded, record);

  if (
    expanded &&
    props.loadTreeData &&
    (!record.children ||
      (record.children as unknown[]).length === 0)
  ) {
    const key = String(record.id || record.key);
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
  <div ref="tableContainerRef" class="pro-table-wrapper">
    <a-table
      v-bind="antTableProps"
      :data-source="dataSource"
      :loading="loading"
      :pagination="pagination"
      :row-selection="rowSelection"
      row-key="id"
      @change="(pag: unknown, filters: unknown, sorter: unknown, extra: unknown) => emit('change', pag, filters, sorter, extra)"
      @expand="handleExpand"
    >
      <template v-if="slots.expandedRowRender" #expandedRowRender="slotProps">
        <slot name="expandedRowRender" v-bind="slotProps"></slot>
      </template>

      <template v-if="slots.summary" #summary="slotProps">
        <slot name="summary" v-bind="slotProps"></slot>
      </template>

      <template #expandIcon="{ expanded, onExpand, record }">
        <template v-if="(record.children && record.children.length > 0) || record.hasChildren">
          <span v-if="loadingKeys.has(record.id || record.key)" class="pro-tree-loading">
            <LoadingOutlined />
          </span>
          <div
            v-else
            :class="['ant-table-row-expand-icon', expanded ? 'ant-table-row-expand-icon-expanded' : 'ant-table-row-expand-icon-collapsed']"
            @click="(e: MouseEvent) => onExpand(record, e)"
          ></div>
        </template>
      </template>

      <template #headerCell="{ column }">
        <slot name="headerCell" :column="column">
          <div class="pro-table-header-cell">
            <span class="pro-table-header-title">{{ column.title || column.key }}</span>
            <div
              v-if="column.__resizable"
              class="pro-table-resize-handle"
              @mousedown="startResize(column.key, $event, column.width)"
            ></div>
          </div>
        </slot>
      </template>

      <template #bodyCell="slotProps">
        <template v-if="slotProps.column.ellipsis && slotProps.column.tooltip">
          <a-tooltip :title="slotProps.record[slotProps.column.dataIndex || slotProps.column.key]">
            <div class="ellipsis-text">
              <slot :name="`bodyCell-${slotProps.column.key}`" v-bind="slotProps">
                <slot name="bodyCell" v-bind="slotProps">
                  {{ slotProps.record[slotProps.column.dataIndex || slotProps.column.key] }}
                </slot>
              </slot>
            </div>
          </a-tooltip>
        </template>
        <template v-else>
          <slot :name="`bodyCell-${slotProps.column.key}`" v-bind="slotProps">
            <slot name="bodyCell" v-bind="slotProps">
              {{ slotProps.record[slotProps.column.dataIndex || slotProps.column.key] }}
            </slot>
          </slot>
        </template>
      </template>

      <template v-for="(_, name) in slots" #[name]="slotData" :key="name">
        <slot
          v-if="name !== 'headerCell' && name !== 'bodyCell' && !String(name).startsWith('bodyCell-')"
          :name="name"
          v-bind="slotData || {}"
        ></slot>
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
  padding-right: 0 !important;
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
  content: "";
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
