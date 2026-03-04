<template>
  <a-card title="我的抄送" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-select v-model:value="readFilter" style="width: 150px" :options="readOptions" />
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
        <template v-if="column.key === 'isRead'">
          <a-tag :color="record.isRead ? 'green' : 'orange'">
            {{ record.isRead ? '已读' : '未读' }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="viewInstance(record.instanceId)">查看流程</a-button>
            <a-button
              v-if="!record.isRead"
              type="link"
              size="small"
              @click="markRead(record.id)"
            >
              标记已读
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import type { ApprovalCopyRecordResponse } from '@/types/api';
import { getMyCopyRecordsPaged, markCopyRecordAsRead } from '@/services/api';

const router = useRouter();
const columns = [
  { title: '实例ID', dataIndex: 'instanceId', key: 'instanceId' },
  { title: '节点ID', dataIndex: 'nodeId', key: 'nodeId' },
  { title: '状态', key: 'isRead' },
  { title: '抄送时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: '操作', key: 'action', width: 220 },
];

const dataSource = ref<ApprovalCopyRecordResponse[]>([]);
const loading = ref(false);
const readFilter = ref<'all' | 'read' | 'unread'>('all');
const readOptions = [
  { label: '全部', value: 'all' },
  { label: '已读', value: 'read' },
  { label: '未读', value: 'unread' },
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
    const isRead = readFilter.value === 'all' ? undefined : readFilter.value === 'read';
    const result = await getMyCopyRecordsPaged(
      {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 10,
      },
      isRead,
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

const markRead = async (copyRecordId: string | number) => {
  try {
    await markCopyRecordAsRead(String(copyRecordId));
    message.success('已标记为已读');
    await fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '操作失败');
  }
};

const viewInstance = (instanceId: string | number) => {
  router.push(`/process/instances/${instanceId}`);
};

onMounted(() => {
  void fetchData();
});

watch(readFilter, () => {
  pagination.current = 1;
  void fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
