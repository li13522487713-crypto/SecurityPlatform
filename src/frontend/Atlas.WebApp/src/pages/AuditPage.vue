<template>
  <a-card title="审计日志" class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索账号/行为/目标"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-select
          v-model:value="resultFilter"
          style="width: 120px"
          :options="resultOptions"
          @change="handleSearch"
        />
        <a-button @click="handleSearch">查询</a-button>
        <a-button @click="handleReset">重置</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :locale="{ emptyText: '暂无审计记录' }"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, text, record }">
        <template v-if="column.key === 'occurredAt'">
          {{ formatDateTime(text) }}
        </template>
        <template v-else-if="column.key === 'result'">
          <a-tag :color="record.result === 'Success' ? 'green' : 'red'">
            {{ record.result === 'Success' ? '成功' : '失败' }}
          </a-tag>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { getAuditsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { formatDateTime } from "@/utils/common";

interface AuditRow {
  id: string;
  actor: string;
  action: string;
  result: string;
  target: string;
  ipAddress?: string;
  occurredAt: string;
}

const columns = [
  { title: "账号", dataIndex: "actor", key: "actor" },
  { title: "行为", dataIndex: "action", key: "action" },
  { title: "结果", dataIndex: "result", key: "result", width: 80 },
  { title: "目标", dataIndex: "target", key: "target", ellipsis: true },
  { title: "IP", dataIndex: "ipAddress", key: "ipAddress" },
  { title: "时间", dataIndex: "occurredAt", key: "occurredAt", width: 180 }
];

const resultOptions = [
  { label: "全部结果", value: "all" },
  { label: "成功", value: "Success" },
  { label: "失败", value: "Failure" }
];

const keyword = ref("");
const resultFilter = ref<string>("all");
const dataSource = ref<AuditRow[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAuditsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || "查询失败");
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
  resultFilter.value = "all";
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
