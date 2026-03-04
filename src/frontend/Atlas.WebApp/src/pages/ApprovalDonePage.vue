<template>
  <a-card title="我的已办" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          allow-clear
          style="width: 240px"
          placeholder="按任务标题检索"
          @pressEnter="fetchData"
        />
        <a-select v-model:value="statusFilter" style="width: 140px" :options="statusOptions" />
        <a-button @click="fetchData">刷新</a-button>
      </a-space>
    </div>
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.status)">
            {{ getStatusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'sla'">
          <a-tag v-if="record.slaRemainingMinutes != null" :color="record.slaRemainingMinutes >= 0 ? 'processing' : 'error'">
            {{ formatSla(record.slaRemainingMinutes) }}
          </a-tag>
          <span v-else>-</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-button type="link" size="small" @click="handleView(record.id)">详情</a-button>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { ApprovalTaskStatus, type ApprovalTaskResponse } from '@/types/api';
import { getMyTasksPaged } from '@/services/api';

const router = useRouter();
const columns = [
  { title: '流程名称', dataIndex: 'flowName', key: 'flowName' },
  { title: '任务标题', dataIndex: 'title', key: 'title' },
  { title: '当前节点', dataIndex: 'currentNodeName', key: 'currentNodeName' },
  { title: 'SLA', key: 'sla' },
  { title: '状态', key: 'status' },
  { title: '处理时间', dataIndex: 'decisionAt', key: 'decisionAt' },
  { title: '操作', key: 'action', width: 120 },
];

const dataSource = ref<ApprovalTaskResponse[]>([]);
const loading = ref(false);
const keyword = ref('');
const statusFilter = ref<ApprovalTaskStatus | 'all'>('all');
const statusOptions = [
  { label: '全部', value: 'all' },
  { label: '已同意', value: ApprovalTaskStatus.Approved },
  { label: '已驳回', value: ApprovalTaskStatus.Rejected },
  { label: '已取消', value: ApprovalTaskStatus.Canceled },
];
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`,
});

const fetchData = async () => {
  loading.value = true;
  try {
    const statusValue = statusFilter.value === 'all' ? undefined : statusFilter.value;
    const result = await getMyTasksPaged(
      {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 10,
        keyword: keyword.value || undefined,
      },
      statusValue ?? ApprovalTaskStatus.Approved,
    );
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : '查询失败');
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
};

const getStatusColor = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Approved:
      return 'green';
    case ApprovalTaskStatus.Rejected:
      return 'red';
    case ApprovalTaskStatus.Canceled:
      return 'default';
    case ApprovalTaskStatus.Delegated:
      return 'purple';
    default:
      return 'default';
  }
};

const getStatusText = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Approved:
      return '已同意';
    case ApprovalTaskStatus.Rejected:
      return '已驳回';
    case ApprovalTaskStatus.Canceled:
      return '已取消';
    case ApprovalTaskStatus.Delegated:
      return '已委派';
    default:
      return '处理中';
  }
};

const formatSla = (value: number) => {
  const abs = Math.abs(value);
  if (abs >= 60) {
    const hours = Math.floor(abs / 60);
    const minutes = abs % 60;
    return value >= 0 ? `剩余 ${hours}h${minutes}m` : `超时 ${hours}h${minutes}m`;
  }
  return value >= 0 ? `剩余 ${abs}m` : `超时 ${abs}m`;
};

const handleView = (taskId: string) => {
  router.push(`/process/tasks/${taskId}`);
};

onMounted(() => {
  void fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
