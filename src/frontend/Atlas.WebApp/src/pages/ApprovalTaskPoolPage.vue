<template>
  <div class="task-pool-page">
    <div class="page-header">
      <h2>公共任务池</h2>
      <a-button type="primary" @click="refresh">刷新</a-button>
    </div>
    <a-table
      :columns="columns"
      :data-source="tasks"
      :loading="loading"
      :pagination="pagination"
      @change="handleTableChange"
      row-key="id"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'action'">
          <a-button type="link" @click="handleClaim(record)">认领</a-button>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag color="blue">待认领</a-tag>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { getTaskPool, claimTask } from '@/services/api';
import type { ApprovalTaskResponse } from '@/types/api';

const loading = ref(false);
const tasks = ref<ApprovalTaskResponse[]>([]);
const pagination = ref({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showQuickJumper: true
});

const columns = [
  { title: '任务标题', dataIndex: 'title', key: 'title' },
  { title: '流程名称', dataIndex: 'flowName', key: 'flowName' }, // 假设 API 返回 flowName
  { title: '当前节点', dataIndex: 'nodeName', key: 'nodeName' }, // 假设 API 返回 nodeName
  { title: '送达时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: '状态', dataIndex: 'status', key: 'status' },
  { title: '操作', key: 'action', width: 100 }
];

const fetchTasks = async () => {
  loading.value = true;
  try {
    const res = await getTaskPool({
      pageIndex: pagination.value.current,
      pageSize: pagination.value.pageSize
    });
    tasks.value = res.items;
    pagination.value.total = res.total;
  } catch (error) {
    message.error('获取任务池失败');
  } finally {
    loading.value = false;
  }
};

const handleTableChange = (pag: TablePaginationConfig) => {
  pagination.value.current = pag.current;
  pagination.value.pageSize = pag.pageSize;
  fetchTasks();
};

const handleClaim = async (task: ApprovalTaskResponse) => {
  try {
    await claimTask(task.id);
    message.success('认领成功');
    fetchTasks();
  } catch (error) {
    message.error('认领失败');
  }
};

const refresh = () => {
  pagination.value.current = 1;
  fetchTasks();
};

onMounted(() => {
  fetchTasks();
});
</script>

<style scoped>
.task-pool-page {
  padding: 24px;
  background: #fff;
}
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
</style>
