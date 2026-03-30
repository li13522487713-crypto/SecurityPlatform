<template>
  <a-card :title="pageTitle" class="page-card">
    <template #extra>
      <a-space>
        <a-button :loading="loading" @click="fetchData">{{ t('dynamic.refresh') }}</a-button>
        <a-button @click="openAttachmentDrawer"><PaperClipOutlined /> {{ t('dynamic.attachments') }}</a-button>
      </a-space>
    </template>

    <QueryGridUnifiedView
      v-model:query-config="advancedQueryConfig"
      :table-config="tableConfig"
      :fields="tableFields"
      :data-source="flatRecords"
      :loading="loading"
      :pagination="pagination"
      :show-query-panel="true"
      :query-title="t('dynamic.advancedSearch')"
      @search="handleSearch"
      @reset="handleReset"
      @change="onTableChange"
    />

    <!-- 附件管理抽屉（表级附件） -->
    <a-drawer
      v-model:open="attachmentDrawerVisible"
      :title="t('dynamic.attachmentDrawerTitle')"
      placement="right"
      :width="480"
      destroy-on-close
    >
      <AttachmentPanel
        :entity-type="tableKey"
        :entity-id="0"
        :allow-multiple="true"
      />
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { PaperClipOutlined } from '@ant-design/icons-vue';
import QueryGridUnifiedView from '@/components/table/QueryGridUnifiedView.vue';
import AttachmentPanel from '@/components/common/attachment-panel.vue';
import { getDynamicTableDetail, getDynamicTableFields, queryDynamicRecords } from '@/services/dynamic-tables';
import type { DynamicFieldDefinition, DynamicRecordDto, DynamicColumnDef, DynamicRecordQueryRequest } from '@/types/dynamic-tables';
import type { AdvancedQueryConfig } from '@/types/advanced-query';
import type { TableViewConfig, TableViewColumnConfig } from '@/types/api';
import { translate } from '@/i18n';

const { t } = useI18n();
const route = useRoute();

// 附件管理抽屉
const attachmentDrawerVisible = ref(false);
const openAttachmentDrawer = () => {
  attachmentDrawerVisible.value = true;
};

const tableKey = computed(() => {
  const key = route.params.tableKey;
  return typeof key === 'string' ? key : '';
});

const pageTitle = ref(translate('dynamic.nativeRecordsTitle'));
const loading = ref(false);
const isMounted = ref(false);

// 动态记录数据
const rawRecords = ref<DynamicRecordDto[]>([]);
const serverColumns = ref<DynamicColumnDef[]>([]);
const tableFields = ref<DynamicFieldDefinition[]>([]);
const total = ref(0);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showTotal: (t: number) => translate('crud.totalItems', { total: t })
});

const advancedQueryConfig = ref<AdvancedQueryConfig>({
  rootGroup: { id: 'root', conjunction: 'and', rules: [], groups: [] }
});

// 将记录值数组展平为 key-value 对象，便于 ProTable 渲染
const flatRecords = computed(() => {
  return rawRecords.value.map(record => {
    const flat: Record<string, unknown> = { id: record.id };
    for (const val of record.values) {
      flat[val.field] = val.stringValue ?? val.intValue ?? val.longValue ??
        val.decimalValue ?? val.boolValue ?? val.dateTimeValue ?? val.dateValue ?? null;
    }
    return flat;
  });
});

// 从返回列动态构建 TableViewConfig
const tableConfig = computed<TableViewConfig>(() => {
  const cols: TableViewColumnConfig[] = serverColumns.value.map(col => ({
    key: col.name,
    dataIndex: col.name,
    title: col.label,
    visible: true,
    ellipsis: true,
    resizable: true
  }));
  return { columns: cols };
});

const loadFields = async () => {
  if (!tableKey.value) return;
  try {
    tableFields.value = await getDynamicTableFields(tableKey.value);
  } catch {
    // non-critical, query panel can be empty
  }
};

const loadTitle = async () => {
  if (!tableKey.value) return;
  try {
    const detail = await getDynamicTableDetail(tableKey.value);
    if (detail && isMounted.value) {
      pageTitle.value = detail.displayName ?? translate('dynamic.nativeRecordsTitle');
    }
  } catch {
    // non-critical
  }
};

const fetchData = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    const hasAdvancedRules =
      (advancedQueryConfig.value.rootGroup.rules?.length ?? 0) > 0 ||
      (advancedQueryConfig.value.rootGroup.groups?.length ?? 0) > 0;

    const request: DynamicRecordQueryRequest = {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      filters: [],
      advancedQuery: hasAdvancedRules ? advancedQueryConfig.value : undefined
    };

    const result = await queryDynamicRecords(tableKey.value, request);
    if (!isMounted.value) return;

    rawRecords.value = result.items;
    serverColumns.value = result.columns;
    pagination.total = Number(result.total) || 0;
    total.value = pagination.total;
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t('crud.queryFailed'));
  } finally {
    if (isMounted.value) loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  void fetchData();
};

const handleReset = () => {
  advancedQueryConfig.value = { rootGroup: { id: 'root', conjunction: 'and', rules: [], groups: [] } };
  pagination.current = 1;
  void fetchData();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
};

onMounted(() => {
  isMounted.value = true;
  void Promise.all([loadTitle(), loadFields(), fetchData()]);
});

onUnmounted(() => { isMounted.value = false; });

watch(tableKey, () => {
  pagination.current = 1;
  advancedQueryConfig.value = { rootGroup: { id: 'root', conjunction: 'and', rules: [], groups: [] } };
  void Promise.all([loadTitle(), loadFields(), fetchData()]);
});
</script>
