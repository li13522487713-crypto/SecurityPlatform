<template>
  <div class="records-page">
    <div class="records-header">
      <div class="header-left">
        <a-button @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
          {{ t("dynamicTable.backToList") }}
        </a-button>
        <span class="header-title">{{ tableSummary?.displayName ?? tableKey }} — {{ t("dynamicTable.recordsTitle") }}</span>
        <a-tag v-if="tableSummary" color="blue">{{ tableKey }}</a-tag>
      </div>
      <div class="header-actions">
        <a-input
          v-model:value="keyword"
          :placeholder="t('dynamicTable.searchRecords')"
          allow-clear
          style="width: 240px"
          @press-enter="doSearch"
        >
          <template #prefix><SearchOutlined /></template>
        </a-input>
        <a-button @click="loadRecords">
          <template #icon><ReloadOutlined /></template>
        </a-button>
        <a-button
          v-if="selectedRowKeys.length > 0"
          danger
          @click="handleBatchDelete"
        >
          {{ t("dynamicTable.batchDelete") }} ({{ selectedRowKeys.length }})
        </a-button>
        <a-button type="primary" @click="handleAddRow">
          <template #icon><PlusOutlined /></template>
          {{ t("dynamicTable.addRecord") }}
        </a-button>
      </div>
    </div>

    <div class="records-body">
      <a-spin :spinning="loading">
        <a-table
          :data-source="records"
          :columns="tableColumns"
          :pagination="pagination"
          :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
          :row-class-name="rowClassName"
          row-key="__rowKey"
          size="middle"
          :scroll="{ x: 'max-content' }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record: row }">
            <!-- Actions column -->
            <template v-if="column.key === '__actions'">
              <a-space v-if="row.__editing">
                <a-button type="link" size="small" :loading="row.__saving" @click="handleSaveRow(row)">
                  {{ t("dynamicTable.inlineSave") }}
                </a-button>
                <a-button type="link" size="small" @click="handleCancelEdit(row)">
                  {{ t("common.cancel") }}
                </a-button>
              </a-space>
              <a-space v-else>
                <a @click="handleStartEdit(row)">{{ t("dynamicTable.editRecord") }}</a>
                <a-popconfirm
                  :title="t('dynamicTable.deleteRecordConfirm')"
                  @confirm="handleDeleteRecord(row)"
                >
                  <a style="color: #ff4d4f">{{ t("dynamicTable.deleteRecord") }}</a>
                </a-popconfirm>
              </a-space>
            </template>

            <!-- Editable cell: Bool -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && getFieldType(column.dataIndex) === 'Bool'">
              <a-switch
                :checked="Boolean(row[column.dataIndex])"
                size="small"
                @update:checked="(v: boolean) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: DateTime -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && getFieldType(column.dataIndex) === 'DateTime'">
              <a-date-picker
                :value="row[column.dataIndex] ?? undefined"
                show-time
                size="small"
                style="width: 100%"
                value-format="YYYY-MM-DDTHH:mm:ss"
                @update:value="(v: string) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: Date -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && getFieldType(column.dataIndex) === 'Date'">
              <a-date-picker
                :value="row[column.dataIndex] ?? undefined"
                size="small"
                style="width: 100%"
                value-format="YYYY-MM-DD"
                @update:value="(v: string) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: Int / Long -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && isIntegerType(getFieldType(column.dataIndex))">
              <a-input-number
                :value="row[column.dataIndex] ?? undefined"
                size="small"
                style="width: 100%"
                :precision="0"
                @update:value="(v: number) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: Decimal -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && getFieldType(column.dataIndex) === 'Decimal'">
              <a-input-number
                :value="row[column.dataIndex] ?? undefined"
                size="small"
                style="width: 100%"
                :precision="2"
                @update:value="(v: number) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: Text (multiline) -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex) && getFieldType(column.dataIndex) === 'Text'">
              <a-textarea
                :value="row[column.dataIndex] ?? ''"
                size="small"
                :auto-size="{ minRows: 1, maxRows: 3 }"
                @update:value="(v: string) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Editable cell: String (default) -->
            <template v-else-if="row.__editing && isEditableField(column.dataIndex)">
              <a-input
                :value="row[column.dataIndex] ?? ''"
                size="small"
                @update:value="(v: string) => { row[column.dataIndex] = v; }"
              />
            </template>

            <!-- Read-only display: Bool -->
            <template v-else-if="!row.__editing && getFieldType(column.dataIndex) === 'Bool'">
              <CheckOutlined v-if="row[column.dataIndex]" style="color: #52c41a" />
              <CloseOutlined v-else style="color: #bfbfbf" />
            </template>

            <!-- Read-only display: null -->
            <template v-else-if="!row.__editing && (row[column.dataIndex] === null || row[column.dataIndex] === undefined)">
              <span class="cell-null">—</span>
            </template>
          </template>
        </a-table>
      </a-spin>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  SearchOutlined,
  ReloadOutlined,
  PlusOutlined,
  CheckOutlined,
  CloseOutlined
} from "@ant-design/icons-vue";
import { useAppContext } from "@/composables/useAppContext";
import {
  queryDynamicRecords,
  createDynamicRecord,
  updateDynamicRecord,
  deleteDynamicRecord,
  deleteDynamicRecordsBatch,
  getDynamicTableSummary
} from "@/services/api-dynamic-tables";
import type {
  DynamicColumnDef,
  DynamicFieldValueDto,
  DynamicRecordDto,
  DynamicTableSummary
} from "@/types/dynamic-tables";
import type { TablePaginationConfig } from "ant-design-vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const { appKey, appId } = useAppContext();

const tableKey = computed(() => String(route.params.tableKey ?? ""));
const loading = ref(false);
const keyword = ref("");

interface EditableRow extends Record<string, unknown> {
  __rowKey: string;
  __editing: boolean;
  __isNew: boolean;
  __saving: boolean;
  __snapshot: Record<string, unknown> | null;
}

const records = ref<EditableRow[]>([]);
const columns = ref<DynamicColumnDef[]>([]);
const selectedRowKeys = ref<string[]>([]);
const tableSummary = ref<DynamicTableSummary | null>(null);

let rowKeySeq = 0;

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `${total}`
});

const SYSTEM_FIELDS = new Set(["id", "created_at", "updated_at", "created_by", "updated_by", "is_deleted"]);
const isSystemField = (name: string) => SYSTEM_FIELDS.has(name.toLowerCase());
const isIntegerType = (type: string) => type === "Int" || type === "Long";

const editableColumns = computed(() =>
  columns.value.filter((col) => !isSystemField(col.name))
);

const getFieldType = (dataIndex: string): string =>
  columns.value.find((c) => c.name === dataIndex)?.type ?? "String";

const isEditableField = (dataIndex: string) => !isSystemField(dataIndex);

const tableColumns = computed(() => {
  const cols = columns.value.map((col) => ({
    title: col.label || col.name,
    dataIndex: col.name,
    key: col.name,
    sorter: col.sortable,
    ellipsis: col.type !== "Bool",
    width: columnWidth(col),
    align: col.type === "Bool" ? ("center" as const) : undefined
  }));
  cols.push({
    title: t("common.actions"),
    dataIndex: "__actions",
    key: "__actions",
    sorter: false,
    ellipsis: false,
    width: 160,
    align: "center" as const
  });
  return cols;
});

function columnWidth(col: DynamicColumnDef): number | undefined {
  if (col.type === "Bool") return 80;
  if (col.type === "DateTime") return 200;
  if (col.type === "Date") return 140;
  if (isIntegerType(col.type) || col.type === "Decimal") return 130;
  return undefined;
}

function rowClassName(row: EditableRow) {
  if (row.__isNew) return "row-new";
  if (row.__editing) return "row-editing";
  return "";
}

// ---------------------------------------------------------------------------
// Data helpers
// ---------------------------------------------------------------------------
function flattenRecord(rec: DynamicRecordDto): EditableRow {
  const row: EditableRow = {
    __rowKey: `r-${++rowKeySeq}`,
    __editing: false,
    __isNew: false,
    __saving: false,
    __snapshot: null,
    id: rec.id
  };
  for (const v of rec.values) {
    if (v.boolValue !== undefined && v.valueType === "Bool") row[v.field] = v.boolValue;
    else if (v.intValue !== undefined && v.valueType === "Int") row[v.field] = v.intValue;
    else if (v.longValue !== undefined && v.valueType === "Long") row[v.field] = v.longValue;
    else if (v.decimalValue !== undefined && v.valueType === "Decimal") row[v.field] = v.decimalValue;
    else if (v.dateTimeValue !== undefined) row[v.field] = v.dateTimeValue;
    else if (v.dateValue !== undefined) row[v.field] = v.dateValue;
    else row[v.field] = v.stringValue ?? null;
  }
  return row;
}

function buildFieldValues(row: EditableRow): DynamicFieldValueDto[] {
  return editableColumns.value.map((col) => {
    const val = row[col.name];
    const dto: DynamicFieldValueDto = { field: col.name, valueType: col.type as DynamicFieldValueDto["valueType"] };
    if (col.type === "Bool") dto.boolValue = Boolean(val);
    else if (col.type === "Int") dto.intValue = Number(val) || 0;
    else if (col.type === "Long") dto.longValue = Number(val) || 0;
    else if (col.type === "Decimal") dto.decimalValue = Number(val) || 0;
    else if (col.type === "DateTime") dto.dateTimeValue = val != null ? String(val) : undefined;
    else if (col.type === "Date") dto.dateValue = val != null ? String(val) : undefined;
    else dto.stringValue = val != null ? String(val) : "";
    return dto;
  });
}

// ---------------------------------------------------------------------------
// CRUD
// ---------------------------------------------------------------------------
const loadRecords = async () => {
  if (!tableKey.value || !appKey.value) return;
  loading.value = true;
  try {
    const result = await queryDynamicRecords(appKey.value, tableKey.value, {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value.trim() || undefined
    }, appId.value ?? undefined);
    columns.value = result.columns;
    records.value = result.items.map(flattenRecord);
    pagination.total = result.total;
    pagination.current = result.pageIndex;
    selectedRowKeys.value = [];
  } catch (err) {
    message.error((err as Error).message || t("dynamicTable.loadRecordsFailed"));
  } finally {
    loading.value = false;
  }
};

const loadSummary = async () => {
  if (!tableKey.value) return;
  try {
    tableSummary.value = await getDynamicTableSummary(tableKey.value, appId.value ?? undefined);
  } catch {
    tableSummary.value = null;
  }
};

const doSearch = () => {
  pagination.current = 1;
  void loadRecords();
};

const handleTableChange = (pag: TablePaginationConfig) => {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  void loadRecords();
};

const onSelectChange = (keys: string[]) => {
  selectedRowKeys.value = keys;
};

const goBack = () => {
  void router.push(`/apps/${appKey.value}/data`);
};

// ---------------------------------------------------------------------------
// Inline editing
// ---------------------------------------------------------------------------
function handleAddRow() {
  const row: EditableRow = {
    __rowKey: `new-${++rowKeySeq}`,
    __editing: true,
    __isNew: true,
    __saving: false,
    __snapshot: null
  };
  for (const col of editableColumns.value) {
    row[col.name] = col.type === "Bool" ? false : null;
  }
  records.value = [row, ...records.value];
}

function handleStartEdit(row: EditableRow) {
  row.__snapshot = {};
  for (const col of editableColumns.value) {
    row.__snapshot[col.name] = row[col.name];
  }
  row.__editing = true;
}

function handleCancelEdit(row: EditableRow) {
  if (row.__isNew) {
    records.value = records.value.filter((r) => r.__rowKey !== row.__rowKey);
    return;
  }
  if (row.__snapshot) {
    for (const col of editableColumns.value) {
      row[col.name] = row.__snapshot[col.name];
    }
  }
  row.__editing = false;
  row.__snapshot = null;
}

async function handleSaveRow(row: EditableRow) {
  if (!appKey.value || !tableKey.value) return;
  row.__saving = true;
  try {
    const values = buildFieldValues(row);
    if (row.__isNew) {
      await createDynamicRecord(appKey.value, tableKey.value, { values }, appId.value ?? undefined);
      message.success(t("dynamicTable.createRecordSuccess"));
    } else {
      await updateDynamicRecord(appKey.value, tableKey.value, String(row.id), { values }, appId.value ?? undefined);
      message.success(t("dynamicTable.updateRecordSuccess"));
    }
    row.__editing = false;
    row.__snapshot = null;
    void loadRecords();
  } catch (err) {
    message.error(
      (err as Error).message ||
        (row.__isNew ? t("dynamicTable.createRecordFailed") : t("dynamicTable.updateRecordFailed"))
    );
  } finally {
    row.__saving = false;
  }
}

async function handleDeleteRecord(row: EditableRow) {
  if (row.__isNew) {
    records.value = records.value.filter((r) => r.__rowKey !== row.__rowKey);
    return;
  }
  if (!appKey.value || !tableKey.value) return;
  try {
    await deleteDynamicRecord(appKey.value, tableKey.value, String(row.id), appId.value ?? undefined);
    message.success(t("dynamicTable.deleteRecordSuccess"));
    void loadRecords();
  } catch (err) {
    message.error((err as Error).message || t("dynamicTable.deleteRecordFailed"));
  }
}

const handleBatchDelete = () => {
  const count = selectedRowKeys.value.length;
  if (count === 0) return;
  Modal.confirm({
    title: t("dynamicTable.batchDeleteConfirm", { count }),
    onOk: async () => {
      if (!appKey.value || !tableKey.value) return;
      const newRowKeys = selectedRowKeys.value.filter((k) => k.startsWith("new-"));
      const existingIds = selectedRowKeys.value
        .filter((k) => !k.startsWith("new-"))
        .map((k) => {
          const row = records.value.find((r) => r.__rowKey === k);
          return row?.id ? String(row.id) : null;
        })
        .filter(Boolean) as string[];

      if (newRowKeys.length > 0) {
        records.value = records.value.filter((r) => !newRowKeys.includes(r.__rowKey));
      }
      if (existingIds.length > 0) {
        try {
          await deleteDynamicRecordsBatch(appKey.value, tableKey.value, existingIds, appId.value ?? undefined);
          message.success(t("dynamicTable.batchDeleteSuccess"));
          void loadRecords();
        } catch (err) {
          message.error((err as Error).message || t("dynamicTable.batchDeleteFailed"));
        }
      }
      selectedRowKeys.value = [];
    }
  });
};

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------
const initialAppIdReady = ref(!!appId.value);

onMounted(() => {
  void loadSummary();
  if (appId.value) {
    void loadRecords();
  }
});

watch(tableKey, () => {
  pagination.current = 1;
  keyword.value = "";
  void loadSummary();
  if (appId.value) {
    void loadRecords();
  }
});

watch(appId, (next) => {
  if (next && !initialAppIdReady.value) {
    initialAppIdReady.value = true;
    void loadRecords();
  }
});
</script>

<style scoped>
.records-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03);
  overflow: hidden;
}

.records-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 8px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.records-body {
  flex: 1;
  padding: 16px 24px;
  overflow: auto;
}

:deep(.row-new) {
  background: #f6ffed !important;
}

:deep(.row-editing) {
  background: #e6f4ff !important;
}

:deep(.row-new td),
:deep(.row-editing td) {
  padding-top: 4px !important;
  padding-bottom: 4px !important;
}

.cell-null {
  color: #bfbfbf;
  font-style: italic;
}
</style>
