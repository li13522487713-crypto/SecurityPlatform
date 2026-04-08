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
        <a-button type="primary" @click="openRecordForm()">
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
          row-key="id"
          size="middle"
          :scroll="{ x: 'max-content' }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === '__actions'">
              <a-space>
                <a @click="openRecordForm(record)">{{ t("dynamicTable.editRecord") }}</a>
                <a-popconfirm
                  :title="t('dynamicTable.deleteRecordConfirm')"
                  @confirm="handleDeleteRecord(record.id)"
                >
                  <a style="color: #ff4d4f">{{ t("dynamicTable.deleteRecord") }}</a>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-spin>
    </div>

    <a-drawer
      v-model:open="formVisible"
      :title="t('dynamicTable.recordFormTitle')"
      width="520"
      :footer-style="{ textAlign: 'right' }"
    >
      <a-form layout="vertical">
        <a-form-item
          v-for="col in editableColumns"
          :key="col.name"
          :label="col.label || col.name"
        >
          <a-input-number
            v-if="isNumberType(col.type)"
            v-model:value="formModel[col.name]"
            style="width: 100%"
          />
          <a-switch
            v-else-if="col.type === 'Bool'"
            v-model:checked="formModel[col.name]"
          />
          <a-date-picker
            v-else-if="col.type === 'DateTime' || col.type === 'Date'"
            v-model:value="formModel[col.name]"
            :show-time="col.type === 'DateTime'"
            style="width: 100%"
            value-format="YYYY-MM-DDTHH:mm:ss"
          />
          <a-textarea
            v-else-if="col.type === 'Text'"
            v-model:value="formModel[col.name]"
            :rows="3"
          />
          <a-input
            v-else
            v-model:value="formModel[col.name]"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="formVisible = false">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" :loading="submitting" @click="handleSubmitRecord">
            {{ t("common.confirm") }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>
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
  PlusOutlined
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
const submitting = ref(false);
const keyword = ref("");
const records = ref<Record<string, unknown>[]>([]);
const columns = ref<DynamicColumnDef[]>([]);
const selectedRowKeys = ref<string[]>([]);
const tableSummary = ref<DynamicTableSummary | null>(null);
const formVisible = ref(false);
const editingId = ref<string | null>(null);
const formModel = reactive<Record<string, unknown>>({});

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `${total}`
});

const isNumberType = (type: string) => ["Int", "Long", "Decimal"].includes(type);

const editableColumns = computed(() =>
  columns.value.filter((col) => col.name !== "id")
);

const tableColumns = computed(() => {
  const cols = columns.value.map((col) => ({
    title: col.label || col.name,
    dataIndex: col.name,
    key: col.name,
    sorter: col.sortable,
    ellipsis: true
  }));
  cols.push({
    title: t("common.actions"),
    dataIndex: "__actions",
    key: "__actions",
    sorter: false,
    ellipsis: false
  });
  return cols;
});

function flattenRecord(rec: DynamicRecordDto): Record<string, unknown> {
  const row: Record<string, unknown> = { id: rec.id };
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

function buildFieldValues(): DynamicFieldValueDto[] {
  return editableColumns.value.map((col) => {
    const val = formModel[col.name];
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
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.loadRecordsFailed"));
  } finally {
    loading.value = false;
  }
};

const loadSummary = async () => {
  if (!tableKey.value) return;
  try {
    tableSummary.value = await getDynamicTableSummary(tableKey.value);
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

const openRecordForm = (record?: Record<string, unknown>) => {
  Object.keys(formModel).forEach((k) => delete formModel[k]);
  if (record) {
    editingId.value = String(record.id);
    for (const col of columns.value) {
      formModel[col.name] = record[col.name] ?? null;
    }
  } else {
    editingId.value = null;
    for (const col of editableColumns.value) {
      formModel[col.name] = col.type === "Bool" ? false : null;
    }
  }
  formVisible.value = true;
};

const handleSubmitRecord = async () => {
  if (!appKey.value || !tableKey.value) return;
  submitting.value = true;
  try {
    const values = buildFieldValues();
    if (editingId.value) {
      await updateDynamicRecord(appKey.value, tableKey.value, editingId.value, { values }, appId.value ?? undefined);
      message.success(t("dynamicTable.updateRecordSuccess"));
    } else {
      await createDynamicRecord(appKey.value, tableKey.value, { values }, appId.value ?? undefined);
      message.success(t("dynamicTable.createRecordSuccess"));
    }
    formVisible.value = false;
    void loadRecords();
  } catch (error) {
    message.error(
      (error as Error).message ||
        (editingId.value ? t("dynamicTable.updateRecordFailed") : t("dynamicTable.createRecordFailed"))
    );
  } finally {
    submitting.value = false;
  }
};

const handleDeleteRecord = async (id: string) => {
  if (!appKey.value || !tableKey.value) return;
  try {
    await deleteDynamicRecord(appKey.value, tableKey.value, id, appId.value ?? undefined);
    message.success(t("dynamicTable.deleteRecordSuccess"));
    void loadRecords();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.deleteRecordFailed"));
  }
};

const handleBatchDelete = () => {
  const count = selectedRowKeys.value.length;
  if (count === 0) return;
  Modal.confirm({
    title: t("dynamicTable.batchDeleteConfirm", { count }),
    onOk: async () => {
      if (!appKey.value || !tableKey.value) return;
      try {
        await deleteDynamicRecordsBatch(appKey.value, tableKey.value, selectedRowKeys.value, appId.value ?? undefined);
        message.success(t("dynamicTable.batchDeleteSuccess"));
        void loadRecords();
      } catch (error) {
        message.error((error as Error).message || t("dynamicTable.batchDeleteFailed"));
      }
    }
  });
};

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
</style>
