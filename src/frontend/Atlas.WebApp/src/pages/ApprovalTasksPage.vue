<template>
  <a-card title="我的待办" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          allow-clear
          style="width: 220px"
          placeholder="按标题或节点关键词检索"
          @pressEnter="fetchData"
        />
        <a-select
          v-model:value="statusFilter"
          style="width: 140px"
          :options="statusOptions"
        />
        <a-select
          v-model:value="selectedAppId"
          style="width: 260px"
          :loading="appLoading"
          :options="appOptions"
          allow-clear
          show-search
          placeholder="按应用过滤"
          @change="handleAppScopeChange"
        />
        <a-button @click="fetchData">刷新</a-button>
      </a-space>
    </div>
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :scroll="isMobile ? { x: 640 } : undefined"
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
          <a-space>
            <a-button type="link" size="small" @click="handleView(record)">详情</a-button>
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

    <a-drawer
      v-model:open="modalVisible"
      :title="modalTitle"
      placement="right"
      width="480"
      destroy-on-close
      @close="handleModalCancel"
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
      <template #footer>
        <a-space>
          <a-button @click="handleModalCancel">取消</a-button>
          <a-button type="primary" @click="handleDecide">确认</a-button>
        </a-space>
      </template>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { getMyTasksPaged, decideApprovalTask } from "@/services/api";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import type { TablePaginationConfig } from "ant-design-vue";
import { ApprovalTaskStatus, type ApprovalTaskResponse } from "@/types/api";
import { message } from "ant-design-vue";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";

const router = useRouter();

const desktopColumns = [
  { title: "流程名称", dataIndex: "flowName", key: "flowName" },
  { title: "任务标题", dataIndex: "title", key: "title" },
  { title: "当前节点", dataIndex: "currentNodeName", key: "currentNodeName" },
  { title: "SLA", key: "sla" },
  { title: "状态", key: "status" },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt" },
  { title: "操作", key: "action", width: 220 }
];
const mobileColumns = [
  { title: "任务标题", dataIndex: "title", key: "title" },
  { title: "状态", key: "status", width: 90 },
  { title: "操作", key: "action", width: 170 }
];
const isMobile = computed(() => window.innerWidth <= 768);
const columns = computed(() => (isMobile.value ? mobileColumns : desktopColumns));

const dataSource = ref<ApprovalTaskResponse[]>([]);
const loading = ref(false);
const appLoading = ref(false);
const keyword = ref("");
const statusFilter = ref<ApprovalTaskStatus | "all">(ApprovalTaskStatus.Pending);
const selectedAppId = ref<string | undefined>(getCurrentAppIdFromStorage() ?? undefined);
const appOptions = ref<Array<{ label: string; value: string }>>([]);
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
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    }, statusValue);
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "查询失败");
  } finally {
    loading.value = false;
  }
};

const loadAppOptions = async () => {
  appLoading.value = true;
  try {
    const result = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 200 });
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }));
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载应用列表失败");
  } finally {
    appLoading.value = false;
  }
};

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value);
  pagination.current = 1;
  void fetchData();
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

const formatSla = (value: number) => {
  const abs = Math.abs(value);
  if (abs >= 60) {
    const hours = Math.floor(abs / 60);
    const minutes = abs % 60;
    return value >= 0 ? `剩余 ${hours}h${minutes}m` : `超时 ${hours}h${minutes}m`;
  }
  return value >= 0 ? `剩余 ${abs}m` : `超时 ${abs}m`;
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

const handleView = (record: ApprovalTaskResponse) => {
  router.push(`/process/tasks/${record.id}`);
};

const handleDecide = async () => {
  if (!currentTask.value) return;

  const approved = modalTitle.value === "审批通过";
  if (!approved && !decideForm.value.comment.trim()) {
    message.warning("驳回时必须填写审批意见");
    return;
  }
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

onMounted(async () => {
  await loadAppOptions();
  await fetchData();
});

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
