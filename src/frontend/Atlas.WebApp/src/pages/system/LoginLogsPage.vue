<template>
  <a-card title="登录日志" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="filters.username"
          placeholder="用户名"
          allow-clear
          style="width: 160px"
        />
        <a-input
          v-model:value="filters.ipAddress"
          placeholder="IP地址"
          allow-clear
          style="width: 160px"
        />
        <a-select
          v-model:value="filters.loginStatus"
          placeholder="登录状态"
          allow-clear
          style="width: 120px"
        >
          <a-select-option :value="true">成功</a-select-option>
          <a-select-option :value="false">失败</a-select-option>
        </a-select>
        <a-range-picker
          v-model:value="filters.timeRange"
          format="YYYY-MM-DD HH:mm"
          show-time
          :placeholder="['开始时间', '结束时间']"
        />
        <a-button type="primary" @click="handleSearch">查询</a-button>
        <a-button @click="handleReset">重置</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      :locale="{ emptyText: '暂无登录日志' }"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'loginStatus'">
          <a-tag :color="record.loginStatus ? 'success' : 'error'">
            {{ record.loginStatus ? '成功' : '失败' }}
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
import { ref, reactive, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { Dayjs } from "dayjs";
import { getLoginLogsPaged, type LoginLogDto } from "@/services/login-log";

const filters = reactive({
  username: "",
  ipAddress: "",
  loginStatus: undefined as boolean | undefined,
  timeRange: null as [Dayjs, Dayjs] | null
});

const dataList = ref<LoginLogDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50", "100"]
});

const columns = [
  { title: "用户名", dataIndex: "username", key: "username" },
  { title: "IP地址", dataIndex: "ipAddress", key: "ipAddress" },
  { title: "浏览器", dataIndex: "browser", key: "browser" },
  { title: "操作系统", dataIndex: "operatingSystem", key: "operatingSystem" },
  { title: "状态", key: "loginStatus", width: 80 },
  { title: "失败原因", dataIndex: "message", key: "message", ellipsis: true },
  { title: "登录时间", key: "loginTime", width: 180 }
];

function formatTime(val: string) {
  if (!val) return "-";
  return new Date(val).toLocaleString("zh-CN", { hour12: false });
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getLoginLogsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      username: filters.username || undefined,
      ipAddress: filters.ipAddress || undefined,
      loginStatus: filters.loginStatus,
      from: filters.timeRange?.[0]?.toISOString(),
      to: filters.timeRange?.[1]?.toISOString()
    });
    dataList.value = result.items as LoginLogDto[];
    pagination.total = Number(result.total);
  } catch (e: any) {
    message.error(e.message || "加载失败");
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

onMounted(() => {
  loadData();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>
