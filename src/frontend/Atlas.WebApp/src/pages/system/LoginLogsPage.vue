<template>
  <a-card :title="t('loginLog.title')" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="filters.username"
          :placeholder="t('loginLog.username')"
          allow-clear
          style="width: 160px"
        />
        <a-input
          v-model:value="filters.ipAddress"
          :placeholder="t('loginLog.ipAddress')"
          allow-clear
          style="width: 160px"
        />
        <a-select
          v-model:value="filters.loginStatus"
          :placeholder="t('loginLog.status')"
          allow-clear
          style="width: 120px"
        >
          <a-select-option :value="true">{{ t("loginLog.statusSuccess") }}</a-select-option>
          <a-select-option :value="false">{{ t("loginLog.statusFailed") }}</a-select-option>
        </a-select>
        <a-range-picker
          v-model:value="filters.timeRange"
          format="YYYY-MM-DD HH:mm"
          show-time
          :placeholder="[t('loginLog.startTime'), t('loginLog.endTime')]"
        />
        <a-button type="primary" @click="handleSearch">查询</a-button>
        <a-button :loading="exporting" @click="handleExport">导出</a-button>
        <a-button @click="handleReset">重置</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      :locale="{ emptyText: t('loginLog.empty') }"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'loginStatus'">
          <a-tag :color="record.loginStatus ? 'success' : 'error'">
            {{ record.loginStatus ? t("loginLog.statusSuccess") : t("loginLog.statusFailed") }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'loginTime'">
          {{ formatTime(record.loginTime) }}
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { TablePaginationConfig } from "ant-design-vue";
import type { Dayjs } from "dayjs";
import { getLoginLogsPaged, exportLoginLogs, type LoginLogDto } from "@/services/login-log";

const { t, locale } = useI18n();

const filters = reactive({
  username: "",
  ipAddress: "",
  loginStatus: undefined as boolean | undefined,
  timeRange: null as [Dayjs, Dayjs] | null
});

const dataList = ref<LoginLogDto[]>([]);
const loading = ref(false);
const exporting = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50", "100"]
});

const columns = [
  { title: t("loginLog.colUsername"), dataIndex: "username", key: "username" },
  { title: t("loginLog.colIpAddress"), dataIndex: "ipAddress", key: "ipAddress" },
  { title: t("loginLog.colBrowser"), dataIndex: "browser", key: "browser" },
  { title: t("loginLog.colOperatingSystem"), dataIndex: "operatingSystem", key: "operatingSystem" },
  { title: t("loginLog.colStatus"), key: "loginStatus", width: 80 },
  { title: t("loginLog.colMessage"), dataIndex: "message", key: "message", ellipsis: true },
  { title: t("loginLog.colLoginTime"), key: "loginTime", width: 180 }
];

function formatTime(val: string) {
  if (!val) return "-";
  const browserLocale = locale.value === "zh" ? "zh-CN" : "en-US";
  return new Date(val).toLocaleString(browserLocale, { hour12: false });
}

async function loadData() {
  loading.value = true;
  try {
    const result  = await getLoginLogsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      username: filters.username || undefined,
      ipAddress: filters.ipAddress || undefined,
      loginStatus: filters.loginStatus,
      from: filters.timeRange?.[0]?.toISOString(),
      to: filters.timeRange?.[1]?.toISOString()
    });

    if (!isMounted.value) return;
    dataList.value = result.items as LoginLogDto[];
    pagination.total = Number(result.total);
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("loginLog.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pagination.current = 1;
  loadData();
}

function handleReset() {
  filters.username = "";
  filters.ipAddress = "";
  filters.loginStatus = undefined;
  filters.timeRange = null;
  pagination.current = 1;
  loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  loadData();
}

async function handleExport() {
  exporting.value = true;
  try {
    const blob  = await exportLoginLogs({
      username: filters.username || undefined,
      ipAddress: filters.ipAddress || undefined,
      loginStatus: filters.loginStatus,
      from: filters.timeRange?.[0]?.toISOString(),
      to: filters.timeRange?.[1]?.toISOString()
    });

    if (!isMounted.value) return;
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `login-logs-${Date.now()}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success(t("loginLogs.exportSuccess"));
  } catch (error: unknown) {
    const requestError = error as { message?: string };
    message.error(requestError.message || t("loginLogs.exportFailed"));
  } finally {
    exporting.value = false;
  }
}

onMounted(() => {
  loadData();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>



