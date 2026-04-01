<template>
  <div class="data-preview-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ tableDisplayName || tableKey }}</span>
        <a-tag color="blue">
          <template #icon><LockOutlined /></template>
          {{ t("dataPreview.readOnly") }}
        </a-tag>
        <a-tooltip v-if="hasSensitiveFields" :title="t('dataPreview.sensitiveHint')">
          <a-tag color="orange">
            <template #icon><ExclamationCircleOutlined /></template>
            {{ t("dataPreview.sensitiveFields") }}
          </a-tag>
        </a-tooltip>
      </div>
      <div class="header-actions">
        <!-- 列显示控制 -->
        <a-dropdown :trigger="['click']">
          <a-button>
            <template #icon><SettingOutlined /></template>
            {{ t("dataPreview.columnFilter") }}
            <DownOutlined />
          </a-button>
          <template #overlay>
            <div class="column-picker-dropdown">
              <div class="column-picker-header">
                <span>{{ t("dataPreview.visibleColumns") }}</span>
                <a-button type="link" size="small" @click="selectAllColumns">{{ t("common.all") }}</a-button>
              </div>
              <a-checkbox-group v-model:value="visibleColumnKeys" class="column-picker-list">
                <div v-for="col in allColumns" :key="col.key" class="column-picker-item">
                  <a-checkbox :value="col.key">{{ col.title }}</a-checkbox>
                </div>
              </a-checkbox-group>
            </div>
          </template>
        </a-dropdown>
        <a-button :loading="loading" @click="fetchData">
          <template #icon><ReloadOutlined /></template>
          {{ t("dynamic.refresh") }}
        </a-button>
      </div>
    </div>

    <!-- 简单搜索栏 -->
    <div class="search-bar">
      <a-input-search
        v-model:value="keyword"
        :placeholder="t('dataPreview.searchPlaceholder')"
        style="max-width: 320px"
        allow-clear
        @search="handleSearch"
        @change="onKeywordChange"
      />
      <span class="record-count">{{ t("dataPreview.totalRecords", { total: totalCount }) }}</span>
    </div>

    <!-- 表格 -->
    <div class="table-container">
      <a-table
        :dataSource="flatRecords"
        :columns="visibleColumns"
        :loading="loading"
        :pagination="{
          current: pageIndex,
          pageSize: pageSize,
          total: totalCount,
          showSizeChanger: true,
          pageSizeOptions: ['10', '20', '50', '100'],
          showTotal: (t: number) => `共 ${t} 条`
        }"
        row-key="id"
        size="small"
        class="preview-table"
        :scroll="{ x: 'max-content' }"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, text }">
          <template v-if="sensitiveFieldKeys.has(String(column.dataIndex))">
            <a-tooltip :title="t('dataPreview.maskedHint')">
              <span class="masked-value">{{ maskValue(String(text ?? '')) }}</span>
            </a-tooltip>
          </template>
        </template>
        <template #emptyText>
          <a-empty :description="t('dataPreview.noRecords')" :image="false" />
        </template>
      </a-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  DownOutlined,
  ExclamationCircleOutlined,
  LockOutlined,
  ReloadOutlined,
  SettingOutlined
} from "@ant-design/icons-vue";
import {
  getDynamicTableDetail,
  getDynamicTableFields,
  queryDynamicRecords
} from "@/services/dynamic-tables";
import type { DynamicColumnDef, DynamicFieldDefinition, DynamicRecordDto, DynamicRecordQueryRequest } from "@/types/dynamic-tables";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? route.params.tableKey : ""));

const tableDisplayName = ref("");
const loading = ref(false);
const isMounted = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const totalCount = ref(0);

const rawRecords = ref<DynamicRecordDto[]>([]);
const serverColumns = ref<DynamicColumnDef[]>([]);
const tableFields = ref<DynamicFieldDefinition[]>([]);
const visibleColumnKeys = ref<string[]>([]);

// 敏感字段识别（包含 password、secret、token 等关键词的字段）
const SENSITIVE_KEYWORDS = ["password", "secret", "token", "phone", "mobile", "id_card", "id_no", "card_no"];
const sensitiveFieldKeys = computed<Set<string>>(() => {
  const keys = new Set<string>();
  for (const f of tableFields.value) {
    const lower = f.name.toLowerCase();
    if (SENSITIVE_KEYWORDS.some((kw) => lower.includes(kw))) {
      keys.add(f.name);
    }
  }
  return keys;
});

const hasSensitiveFields = computed(() => sensitiveFieldKeys.value.size > 0);

const maskValue = (val: string): string => {
  if (!val) return "";
  if (val.length <= 3) return "***";
  return val.slice(0, 2) + "*".repeat(Math.min(4, val.length - 2)) + val.slice(-1);
};

interface TableColumn {
  key: string;
  dataIndex: string;
  title: string;
  ellipsis: boolean;
  width?: number;
}

const allColumns = computed<TableColumn[]>(() => {
  if (serverColumns.value.length > 0) {
    return serverColumns.value.map((col) => ({
      key: col.name,
      dataIndex: col.name,
      title: col.label,
      ellipsis: true,
      width: 160
    }));
  }
  return tableFields.value.map((f) => ({
    key: f.name,
    dataIndex: f.name,
    title: f.displayName ?? f.name,
    ellipsis: true,
    width: 160
  }));
});

const visibleColumns = computed<TableColumn[]>(() => {
  if (visibleColumnKeys.value.length === 0) return allColumns.value;
  return allColumns.value.filter((col) => visibleColumnKeys.value.includes(col.key));
});

const flatRecords = computed(() => {
  return rawRecords.value.map((record) => {
    const flat: Record<string, unknown> = { id: record.id };
    for (const val of record.values) {
      flat[val.field] = val.stringValue ?? val.intValue ?? val.longValue ??
        val.decimalValue ?? val.boolValue ?? val.dateTimeValue ?? val.dateValue ?? null;
    }
    return flat;
  });
});

const selectAllColumns = () => {
  visibleColumnKeys.value = allColumns.value.map((c) => c.key);
};

const loadTableInfo = async () => {
  if (!tableKey.value) return;
  try {
    const [detail, fields] = await Promise.all([
      getDynamicTableDetail(tableKey.value),
      getDynamicTableFields(tableKey.value)
    ]);
    if (detail && isMounted.value) {
      tableDisplayName.value = detail.displayName ?? tableKey.value;
    }
    if (isMounted.value) {
      tableFields.value = fields;
    }
  } catch {
    // non-critical
  }
};

const fetchData = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    const request: DynamicRecordQueryRequest = {
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      filters: keyword.value
        ? [{ field: "_fulltext", operator: "contains", value: keyword.value }]
        : []
    };
    const result = await queryDynamicRecords(tableKey.value, request);
    if (!isMounted.value) return;
    rawRecords.value = result.items;
    serverColumns.value = result.columns;
    totalCount.value = Number(result.total) || 0;
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("dataPreview.loadFailed"));
  } finally {
    if (isMounted.value) loading.value = false;
  }
};

const handleSearch = () => {
  pageIndex.value = 1;
  void fetchData();
};

let keywordTimer: ReturnType<typeof setTimeout> | null = null;
const onKeywordChange = () => {
  if (keywordTimer) clearTimeout(keywordTimer);
  keywordTimer = setTimeout(() => { void handleSearch(); }, 400);
};

const onTableChange = (pager: TablePaginationConfig) => {
  pageIndex.value = pager.current ?? 1;
  pageSize.value = pager.pageSize ?? 20;
  void fetchData();
};

const goBack = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data`);
};

onMounted(() => {
  isMounted.value = true;
  void Promise.all([loadTableInfo(), fetchData()]);
});

onUnmounted(() => {
  isMounted.value = false;
  if (keywordTimer) clearTimeout(keywordTimer);
});

watch(tableKey, () => {
  pageIndex.value = 1;
  keyword.value = "";
  rawRecords.value = [];
  void Promise.all([loadTableInfo(), fetchData()]);
});
</script>

<style scoped>
.data-preview-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0,0,0,.03), 0 1px 6px -1px rgba(0,0,0,.02);
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.search-bar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px 24px;
  border-bottom: 1px solid #f5f5f5;
  background: #fff;
  flex-shrink: 0;
}

.record-count {
  font-size: 13px;
  color: #8c8c8c;
}

.table-container {
  flex: 1;
  overflow: auto;
  padding: 0 24px 24px;
}

.preview-table {
  margin-top: 12px;
}

.masked-value {
  color: #8c8c8c;
  font-family: monospace;
  letter-spacing: 2px;
}

.column-picker-dropdown {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0,0,0,.08);
  padding: 12px;
  min-width: 220px;
  max-height: 360px;
  overflow-y: auto;
}

.column-picker-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: #595959;
  padding-bottom: 8px;
  border-bottom: 1px solid #f0f0f0;
}

.column-picker-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.column-picker-item {
  padding: 3px 0;
}
</style>
