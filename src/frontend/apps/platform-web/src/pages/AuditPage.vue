<template>
  <div style="padding: 24px;">
    <a-card>
      <div class="crud-toolbar">
        <a-space wrap>
          <a-input
            v-model:value="keyword"
            :placeholder="t('auditPage.searchPlaceholder')"
            allow-clear
            @press-enter="handleSearch"
          />
          <a-select
            v-model:value="actionFilter"
            style="width: 180px"
            :options="actionOptions"
            @change="handleSearch"
          />
          <a-select
            v-model:value="resultFilter"
            style="width: 120px"
            :options="resultOptions"
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
        :locale="{ emptyText: t('auditPage.emptyText') }"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, text, record }">
          <template v-if="column.key === 'occurredAt'">
            {{ formatDateTime(text) }}
          </template>
          <template v-else-if="column.key === 'result'">
            <a-tag :color="record.result === 'Success' ? 'green' : 'red'">
              {{ record.result === "Success" ? t("auditPage.resultSuccess") : t("auditPage.resultFailure") }}
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
import { getAuditsPaged } from "@/services/api-system";

interface AuditRow {
  id: string;
  actor: string;
  action: string;
  result: string;
  target: string;
  ipAddress?: string;
  occurredAt: string;
}

const { t, locale } = useI18n();
const isMounted = ref(false);
const keyword = ref("");
const actionFilter = ref<string>("all");
const resultFilter = ref<string>("all");
const dataSource = ref<AuditRow[]>([]);
const loading = ref(false);

const columns = computed(() => [
  { title: t("auditPage.colActor"), dataIndex: "actor", key: "actor" },
  { title: t("auditPage.colAction"), dataIndex: "action", key: "action" },
  { title: t("auditPage.colResult"), dataIndex: "result", key: "result", width: 100 },
  { title: t("auditPage.colTarget"), dataIndex: "target", key: "target", ellipsis: true },
  { title: t("auditPage.colIp"), dataIndex: "ipAddress", key: "ipAddress" },
  { title: t("auditPage.colOccurredAt"), dataIndex: "occurredAt", key: "occurredAt", width: 180 }
]);

const resultOptions = computed(() => [
  { label: t("auditPage.resultAll"), value: "all" },
  { label: t("auditPage.resultSuccess"), value: "Success" },
  { label: t("auditPage.resultFailure"), value: "Failure" }
]);

const actionOptions = computed(() => [
  { label: t("auditPage.actionAll"), value: "all" },
  { label: t("auditPage.actionLogin"), value: "LOGIN" },
  { label: t("auditPage.actionLogout"), value: "LOGOUT" },
  { label: t("auditPage.actionPublishPage"), value: "PUBLISH_PAGE" },
  { label: t("auditPage.actionExecuteMigration"), value: "EXECUTE_MIGRATION" },
  { label: t("auditPage.actionClientError"), value: "CLIENT_ERROR" }
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

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAuditsPaged(
      {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 10,
        keyword: keyword.value || undefined
      },
      {
        action: actionFilter.value === "all" ? undefined : actionFilter.value,
        result: resultFilter.value === "all" ? undefined : resultFilter.value
      }
    );

    if (!isMounted.value) return;
    dataSource.value = result.items as AuditRow[];
    pagination.total = result.total;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("auditPage.queryFailed"));
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
  actionFilter.value = "all";
  resultFilter.value = "all";
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
