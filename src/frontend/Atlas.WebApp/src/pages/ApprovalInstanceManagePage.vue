<template>
  <CrudPageLayout
    v-model:keyword="filters.businessKey"
    title="实例管理"
    search-placeholder="业务Key"
    @search="fetchData"
  >
    <template #search-filters>
      <a-form-item>
        <a-select
          v-model:value="filters.status"
          style="width: 160px"
          :options="statusOptions"
          allow-clear
          placeholder="实例状态"
          @change="fetchData"
        />
      </a-form-item>
    </template>

    <template #table>
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
          <a-tag :color="statusColor(record.status)">
            {{ statusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'sla'">
          <a-tag v-if="record.slaRemainingMinutes != null" :color="record.slaRemainingMinutes >= 0 ? 'processing' : 'error'">
            {{ formatSla(record.slaRemainingMinutes) }}
          </a-tag>
          <span v-else>-</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="viewDetail(record.id)">详情</a-button>
            <a-button
              v-if="record.status === 0"
              type="link"
              size="small"
              danger
              @click="terminate(record.id)"
            >
              终止
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from 'vue';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { ApprovalInstanceStatus, type ApprovalInstanceListItem } from '@/types/api';
import { getAdminInstancesPaged, terminateInstance } from '@/services/api';
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";

const router = useRouter();
const columns = [
  { title: '流程名称', dataIndex: 'flowName', key: 'flowName' },
  { title: '业务Key', dataIndex: 'businessKey', key: 'businessKey' },
  { title: '当前节点', dataIndex: 'currentNodeName', key: 'currentNodeName' },
  { title: 'SLA', key: 'sla' },
  { title: '发起人', dataIndex: 'initiatorUserId', key: 'initiatorUserId' },
  { title: '状态', key: 'status' },
  { title: '发起时间', dataIndex: 'startedAt', key: 'startedAt' },
  { title: '操作', key: 'action', width: 180 },
];

const dataSource = ref<ApprovalInstanceListItem[]>([]);
const loading = ref(false);
const filters = reactive<{ status?: number; businessKey?: string }>({});
const statusOptions = [
  { label: '运行中', value: ApprovalInstanceStatus.Running },
  { label: '已完成', value: ApprovalInstanceStatus.Completed },
  { label: '已驳回', value: ApprovalInstanceStatus.Rejected },
  { label: '已取消', value: ApprovalInstanceStatus.Canceled },
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
    const result  = await getAdminInstancesPaged(
      {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 10,
      },
      {
        status: filters.status,
        businessKey: filters.businessKey,
      },
    );

    if (!isMounted.value) return;
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

const viewDetail = (instanceId: string | number) => {
  router.push(`/process/instances/${instanceId}`);
};

const terminate = async (instanceId: string | number) => {
  try {
    await terminateInstance(String(instanceId), '管理端终止');

    if (!isMounted.value) return;
    message.success('终止成功');
    await fetchData();

    if (!isMounted.value) return;
  } catch (err) {
    message.error(err instanceof Error ? err.message : '终止失败');
  }
};

const statusText = (status: ApprovalInstanceStatus) => {
  switch (status) {
    case ApprovalInstanceStatus.Running:
      return '运行中';
    case ApprovalInstanceStatus.Completed:
      return '已完成';
    case ApprovalInstanceStatus.Rejected:
      return '已驳回';
    case ApprovalInstanceStatus.Canceled:
      return '已取消';
    case ApprovalInstanceStatus.Suspended:
      return '已挂起';
    case ApprovalInstanceStatus.Draft:
      return '草稿';
    case ApprovalInstanceStatus.TimedOut:
      return '已超时';
    case ApprovalInstanceStatus.Terminated:
      return '已终止';
    case ApprovalInstanceStatus.AutoApproved:
      return '自动通过';
    case ApprovalInstanceStatus.AutoRejected:
      return '自动驳回';
    case ApprovalInstanceStatus.AiProcessing:
      return 'AI处理中';
    case ApprovalInstanceStatus.AiManualReview:
      return 'AI转人工';
    case ApprovalInstanceStatus.Destroy:
      return '已作废';
    default:
      return '未知';
  }
};

const statusColor = (status: ApprovalInstanceStatus) => {
  switch (status) {
    case ApprovalInstanceStatus.Running:
      return 'processing';
    case ApprovalInstanceStatus.Completed:
      return 'success';
    case ApprovalInstanceStatus.Rejected:
      return 'error';
    case ApprovalInstanceStatus.Canceled:
      return 'default';
    case ApprovalInstanceStatus.Suspended:
      return 'orange';
    case ApprovalInstanceStatus.Draft:
      return 'purple';
    case ApprovalInstanceStatus.TimedOut:
      return 'volcano';
    case ApprovalInstanceStatus.Terminated:
      return 'magenta';
    case ApprovalInstanceStatus.AutoApproved:
      return 'cyan';
    case ApprovalInstanceStatus.AutoRejected:
      return 'geekblue';
    case ApprovalInstanceStatus.AiProcessing:
      return 'processing';
    case ApprovalInstanceStatus.AiManualReview:
      return 'gold';
    case ApprovalInstanceStatus.Destroy:
      return 'default';
    default:
      return 'default';
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

onMounted(() => {
  void fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
