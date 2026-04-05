<template>
  <div style="padding: 24px;">
    <a-card :title="t('loginLog.title')">
      <div style="margin-bottom: 16px;">
        <a-space wrap>
          <a-input v-model:value="filters.username" :placeholder="t('loginLog.username')" allow-clear style="width: 160px" />
          <a-input v-model:value="filters.ipAddress" :placeholder="t('loginLog.ipAddress')" allow-clear style="width: 160px" />
          <a-select v-model:value="filters.loginStatus" :placeholder="t('loginLog.status')" allow-clear style="width: 120px">
            <a-select-option :value="true">{{ t("loginLog.statusSuccess") }}</a-select-option>
            <a-select-option :value="false">{{ t("loginLog.statusFailed") }}</a-select-option>
          </a-select>
          <a-button type="primary" @click="handleSearch">{{ t("common.search") }}</a-button>
          <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
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
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getLoginLogsPaged } from "@/services/api-login-logs";
import type { LoginLogDto } from "@/services/api-login-logs";

const { t, locale } = useI18n();

const filters = reactive({
  username: "",
  ipAddress: "",
  loginStatus: undefined as boolean | undefined
});

const dataList = ref<LoginLogDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const columns = computed(() => [
  { title: t("loginLog.colUsername"), dataIndex: "username", key: "username" },
  { title: t("loginLog.colIpAddress"), dataIndex: "ipAddress", key: "ipAddress" },
  { title: t("loginLog.colBrowser"), dataIndex: "browser", key: "browser" },
  { title: t("loginLog.colOperatingSystem"), dataIndex: "operatingSystem", key: "operatingSystem" },
  { title: t("loginLog.colStatus"), key: "loginStatus", width: 80 },
  { title: t("loginLog.colMessage"), dataIndex: "message", key: "message", ellipsis: true },
  { title: t("loginLog.colLoginTime"), key: "loginTime", width: 180 }
]);

function formatTime(val: string) {
  if (!val) return "-";
  const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(val).toLocaleString(loc, { hour12: false });
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getLoginLogsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      username: filters.username || undefined,
      ipAddress: filters.ipAddress || undefined,
      loginStatus: filters.loginStatus
    });
    dataList.value = result.items as LoginLogDto[];
    pagination.total = Number(result.total);
  } catch {
    message.error(t("loginLog.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pagination.current = 1;
  void loadData();
}

function handleReset() {
  filters.username = "";
  filters.ipAddress = "";
  filters.loginStatus = undefined;
  pagination.current = 1;
  void loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  void loadData();
}

onMounted(() => void loadData());
</script>
