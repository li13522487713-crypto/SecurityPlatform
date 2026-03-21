<template>
  <a-card class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          :placeholder="t('pages.alert.searchPlaceholder')"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-select
          v-model:value="severityFilter"
          style="width: 120px"
          :options="severityOptions"
          @change="handleSearch"
        />
        <a-button @click="handleSearch">{{ t("common.search") }}</a-button>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :locale="{ emptyText: t('pages.alert.emptyText') }"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, text, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(text) }}
        </template>
        <template v-else-if="column.key === 'severity'">
          <a-tag :color="getSeverityColor(record.severity)">
            {{ record.severity || t("pages.alert.unknownSeverity") }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="record.status === 'Resolved' ? 'green' : 'orange'">
            {{ record.status === "Resolved" ? t("pages.alert.statusResolved") : t("pages.alert.statusOpen") }}
          </a-tag>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { getAlertsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { formatDateTime } from "@/utils/common";

const { t } = useI18n();

interface AlertRow {
  id: string;
  title: string;
  severity?: string;
  status?: string;
  source?: string;
  createdAt: string;
}

const columns = computed(() => [
  { title: t("pages.alert.colTitle"), dataIndex: "title", key: "title" },
  { title: t("pages.alert.colSeverity"), dataIndex: "severity", key: "severity", width: 100 },
  { title: t("pages.alert.colStatus"), dataIndex: "status", key: "status", width: 100 },
  { title: t("pages.alert.colSource"), dataIndex: "source", key: "source" },
  { title: t("pages.alert.colCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 }
]);

const severityOptions = computed(() => [
  { label: t("pages.alert.severityAll"), value: "all" },
  { label: t("pages.alert.severityCritical"), value: "Critical" },
  { label: t("pages.alert.severityHigh"), value: "High" },
  { label: t("pages.alert.severityMedium"), value: "Medium" },
  { label: t("pages.alert.severityLow"), value: "Low" }
]);

const keyword = ref("");
const severityFilter = ref<string>("all");
const dataSource = ref<AlertRow[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});

const getSeverityColor = (severity?: string) => {
  switch (severity) {
    case "Critical":
      return "red";
    case "High":
      return "orange";
    case "Medium":
      return "gold";
    case "Low":
      return "blue";
    default:
      return "default";
  }
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAlertsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("pages.alert.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleReset = () => {
  keyword.value = "";
  severityFilter.value = "all";
  handleSearch();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

onMounted(fetchData);
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
</style>
