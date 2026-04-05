<template>
  <div style="padding: 24px;">
    <a-card>
      <div class="crud-toolbar">
        <a-space wrap>
          <a-input
            v-model:value="keyword"
            :placeholder="t('alertPage.searchPlaceholder')"
            allow-clear
            @press-enter="handleSearch"
          />
          <a-select
            v-model:value="severityFilter"
            style="width: 130px"
            :options="severityOptions"
            @change="handleSearch"
          />
          <a-button type="primary" @click="handleSearch">{{ t("common.search") }}</a-button>
          <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :locale="{ emptyText: t('alertPage.emptyText') }"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, text, record }">
          <template v-if="column.key === 'createdAt'">
            {{ formatDateTime(text) }}
          </template>
          <template v-else-if="column.key === 'severity'">
            <a-tag :color="getSeverityColor(record.severity)">
              {{ record.severity || t("alertPage.unknownSeverity") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="record.status === 'Resolved' ? 'green' : 'orange'">
              {{ record.status === "Resolved" ? t("alertPage.statusResolved") : t("alertPage.statusOpen") }}
            </a-tag>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getAlertsPaged } from "@/services/api-system";

interface AlertRow {
  id: string;
  title: string;
  severity?: string;
  status?: string;
  source?: string;
  createdAt: string;
}

const { t, locale } = useI18n();
const isMounted = ref(false);
const keyword = ref("");
const severityFilter = ref<string>("all");
const dataSource = ref<AlertRow[]>([]);
const loading = ref(false);

const columns = computed(() => [
  { title: t("alertPage.colTitle"), dataIndex: "title", key: "title" },
  { title: t("alertPage.colSeverity"), dataIndex: "severity", key: "severity", width: 120 },
  { title: t("alertPage.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("alertPage.colSource"), dataIndex: "source", key: "source" },
  { title: t("alertPage.colCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 }
]);

const severityOptions = computed(() => [
  { label: t("alertPage.severityAll"), value: "all" },
  { label: t("alertPage.severityCritical"), value: "Critical" },
  { label: t("alertPage.severityHigh"), value: "High" },
  { label: t("alertPage.severityMedium"), value: "Medium" },
  { label: t("alertPage.severityLow"), value: "Low" }
]);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});

function formatDateTime(value?: string) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const currentLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return date.toLocaleString(currentLocale, { hour12: false });
}

function getSeverityColor(severity?: string) {
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
}

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAlertsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      severity: severityFilter.value === "all" ? undefined : severityFilter.value
    });

    if (!isMounted.value) return;
    dataSource.value = result.items as AlertRow[];
    pagination.total = result.total;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("alertPage.queryFailed"));
  } finally {
    loading.value = false;
  }
};

function handleSearch() {
  pagination.current = 1;
  void fetchData();
}

function handleReset() {
  keyword.value = "";
  severityFilter.value = "all";
  handleSearch();
}

function onTableChange(pager: TablePaginationConfig) {
  pagination.current = pager.current ?? 1;
  pagination.pageSize = pager.pageSize ?? 10;
  void fetchData();
}

onMounted(() => {
  isMounted.value = true;
  void fetchData();
});

onUnmounted(() => {
  isMounted.value = false;
});
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
