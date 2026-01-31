<template>
  <a-card title="我的待办" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-select
          v-model:value="statusFilter"
          style="width: 140px"
          :options="statusOptions"
        />
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
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button
              v-if="record.status === 0"
              type="primary"
              size="small"
              @click="handleApprove(record)"
            >
              审批
            </a-button>
            <a-button
              v-if="record.status === 0"
              danger
              size="small"
              @click="handleReject(record)"
            >
              驳回
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="modalTitle"
      @ok="handleDecide"
      @cancel="handleModalCancel"
    >
      <a-form :model="decideForm" layout="vertical">
        <a-form-item label="审批意见">
          <a-textarea
            v-model:value="decideForm.comment"
            :rows="4"
            placeholder="请输入审批意见"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from "vue";
import { getMyTasksPaged, decideApprovalTask } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { ApprovalTaskStatus, type ApprovalTaskResponse } from "@/types/api";
import { message } from "ant-design-vue";

const columns = [
  { title: "任务标题", dataIndex: "title", key: "title" },
  { title: "节点ID", dataIndex: "nodeId", key: "nodeId" },
  { title: "状态", key: "status" },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt" },
  { title: "操作", key: "action", width: 150 }
];

const dataSource = ref<ApprovalTaskResponse[]>([]);
const loading = ref(false);
const statusFilter = ref<ApprovalTaskStatus | "all">(ApprovalTaskStatus.Pending);
const statusOptions = [
  { label: "全部", value: "all" },
  { label: "待审批", value: ApprovalTaskStatus.Pending },
  { label: "已同意", value: ApprovalTaskStatus.Approved },
  { label: "已驳回", value: ApprovalTaskStatus.Rejected },
  { label: "已取消", value: ApprovalTaskStatus.Canceled }
];
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const modalVisible = ref(false);
const modalTitle = ref("");
const currentTask = ref<ApprovalTaskResponse | null>(null);
const decideForm = ref({ comment: "" });

const fetchData = async () => {
  loading.value = true;
  try {
    const statusValue = statusFilter.value === "all" ? undefined : statusFilter.value;
    const result = await getMyTasksPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    }, statusValue);
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "查询失败");
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const getStatusColor = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Pending:
      return "orange";
    case ApprovalTaskStatus.Approved:
      return "green";
    case ApprovalTaskStatus.Rejected:
      return "red";
    case ApprovalTaskStatus.Canceled:
      return "default";
    default:
      return "default";
  }
};

const getStatusText = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Pending:
      return "待审批";
    case ApprovalTaskStatus.Approved:
      return "已同意";
    case ApprovalTaskStatus.Rejected:
      return "已驳回";
    case ApprovalTaskStatus.Canceled:
      return "已取消";
    default:
      return "未知";
  }
};

const handleApprove = (record: ApprovalTaskResponse) => {
  currentTask.value = record;
  modalTitle.value = "审批通过";
  decideForm.value.comment = "";
  modalVisible.value = true;
};

const handleReject = (record: ApprovalTaskResponse) => {
  currentTask.value = record;
  modalTitle.value = "驳回";
  decideForm.value.comment = "";
  modalVisible.value = true;
};

const handleDecide = async () => {
  if (!currentTask.value) return;

  const approved = modalTitle.value === "审批通过";
  try {
    await decideApprovalTask({
      taskId: currentTask.value.id,
      approved,
      comment: decideForm.value.comment || undefined
    });
    message.success(approved ? "审批成功" : "驳回成功");
    modalVisible.value = false;
    currentTask.value = null;
    decideForm.value.comment = "";
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "操作失败");
  }
};

const handleModalCancel = () => {
  modalVisible.value = false;
  currentTask.value = null;
  decideForm.value.comment = "";
};

onMounted(fetchData);

watch(statusFilter, () => {
  pagination.current = 1;
  fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
