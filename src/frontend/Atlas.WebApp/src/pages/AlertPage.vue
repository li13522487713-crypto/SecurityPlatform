<template>
  <a-card class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索告警标题"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-select
          v-model:value="severityFilter"
          style="width: 120px"
          :options="severityOptions"
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
      :locale="{ emptyText: '暂无告警记录' }"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, text, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(text) }}
        </template>
        <template v-else-if="column.key === 'severity'">
          <a-tag :color="getSeverityColor(record.severity)">
            {{ record.severity || '未知' }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="record.status === 'Resolved' ? 'green' : 'orange'">
            {{ record.status === 'Resolved' ? '已解决' : '待处理' }}
          </a-tag>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { getAlertsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { formatDateTime } from "@/utils/common";

interface AlertRow {
  id: string;
  title: string;
  severity?: string;
  status?: string;
  source?: string;
  createdAt: string;
}

const columns = [
  { title: "标题", dataIndex: "title", key: "title" },
  { title: "严重程度", dataIndex: "severity", key: "severity", width: 100 },
  { title: "状态", dataIndex: "status", key: "status", width: 100 },
  { title: "来源", dataIndex: "source", key: "source" },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt", width: 180 }
];

const severityOptions = [
  { label: "全部等级", value: "all" },
  { label: "严重", value: "Critical" },
  { label: "高危", value: "High" },
  { label: "中危", value: "Medium" },
  { label: "低危", value: "Low" }
];

const keyword = ref("");
const severityFilter = ref<string>("all");
const dataSource = ref<AlertRow[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const getSeverityColor = (severity?: string) => {
  switch (severity) {
    case "Critical": return "red";
    case "High": return "orange";
    case "Medium": return "gold";
    case "Low": return "blue";
    default: return "default";
  }
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result  = await getAlertsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
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
