<template>
  <a-card :bordered="false" class="page-card tab-content-card">
    <FilterToolbar
      :show-refresh="true"
      @refresh="fetchData"
    >
      <a-select
        v-model:value="statusFilter"
        style="width: 140px"
        :options="statusOptions"
        @change="handleFilterUpdate"
      />
    </FilterToolbar>
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
          <a-space>
            <a-button type="link" size="small" @click="handleViewDetail(record.id)">查看详情</a-button>
            <a-button
              v-if="record.status === 0"
              type="link"
              size="small"
              danger
              @click="handleCancel(record.id)"
            >
              取消
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="drawerVisible"
      title="流程详情"
      placement="right"
      width="600"
      @close="handleDrawerClose"
    >
      <div v-if="instanceDetail">
        <a-descriptions :column="1" bordered>
          <a-descriptions-item label="流程名称">{{ instanceDetail.flowName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="业务Key">{{ instanceDetail.businessKey }}</a-descriptions-item>
          <a-descriptions-item label="当前节点">{{ instanceDetail.currentNodeName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="SLA">
            <a-tag v-if="instanceDetail.slaRemainingMinutes != null" :color="instanceDetail.slaRemainingMinutes >= 0 ? 'processing' : 'error'">
              {{ formatSla(instanceDetail.slaRemainingMinutes) }}
            </a-tag>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="getStatusColor(instanceDetail.status)">
              {{ getStatusText(instanceDetail.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="发起时间">{{ instanceDetail.startedAt }}</a-descriptions-item>
          <a-descriptions-item v-if="instanceDetail.endedAt" label="结束时间">
            {{ instanceDetail.endedAt }}
          </a-descriptions-item>
        </a-descriptions>

        <!-- 动态表业务数据展示 -->
        <template v-if="businessData && businessData.length > 0">
          <a-divider>业务数据</a-divider>
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item
              v-for="item in businessData"
              :key="item.field"
              :label="item.field"
            >
              {{ item.value ?? '-' }}
            </a-descriptions-item>
          </a-descriptions>
        </template>

        <a-divider>任务列表</a-divider>
        <a-table
          :columns="taskColumns"
          :data-source="taskList"
          :loading="taskLoading"
          row-key="id"
          :pagination="false"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <a-tag :color="getTaskStatusColor(record.status)">
                {{ getTaskStatusText(record.status) }}
              </a-tag>
            </template>
          </template>
        </a-table>

        <a-divider>操作历史</a-divider>
        <a-timeline>
          <a-timeline-item v-for="event in historyList" :key="event.id">
            <p>{{ event.eventType }}</p>
            <p v-if="event.fromNode || event.toNode" style="color: #999; font-size: 12px">
              {{ event.fromNode }} → {{ event.toNode }}
            </p>
            <p style="color: #999; font-size: 12px">{{ event.occurredAt }}</p>
          </a-timeline-item>
        </a-timeline>
      </div>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from "vue";
import {
  getMyInstancesPaged,
  getApprovalInstanceById,
  getApprovalTasksByInstance,
  getApprovalInstanceHistory,
  cancelApprovalInstance
} from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  ApprovalInstanceStatus,
  ApprovalTaskStatus,
  type ApprovalInstanceListItem,
  type ApprovalInstanceResponse,
  type ApprovalTaskResponse,
  type ApprovalHistoryEventResponse
} from "@/types/api";
import { message } from "ant-design-vue";
import FilterToolbar from "@/components/common/FilterToolbar.vue";

const props = defineProps<{
  urlKeyword?: string;
  urlStatus?: string;
}>();

const emit = defineEmits<{
  'update-filter': [{keyword: string, status: string}];
}>();

const columns = [
  { title: "流程名称", dataIndex: "flowName", key: "flowName" },
  { title: "业务Key", dataIndex: "businessKey", key: "businessKey" },
  { title: "当前节点", dataIndex: "currentNodeName", key: "currentNodeName" },
  { title: "SLA", key: "sla" },
  { title: "状态", key: "status" },
  { title: "发起时间", dataIndex: "startedAt", key: "startedAt" },
  { title: "操作", key: "action", width: 150 }
];

const taskColumns = [
  { title: "任务标题", dataIndex: "title", key: "title" },
  { title: "节点ID", dataIndex: "nodeId", key: "nodeId" },
  { title: "状态", key: "status" },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt" }
];

const dataSource = ref<ApprovalInstanceListItem[]>([]);
const loading = ref(false);
const statusFilter = ref<ApprovalInstanceStatus | "all">((props.urlStatus as unknown as ApprovalInstanceStatus) || "all");
const statusOptions = [
  { label: "全部", value: "all" },
  { label: "运行中", value: ApprovalInstanceStatus.Running },
  { label: "已完成", value: ApprovalInstanceStatus.Completed },
  { label: "已驳回", value: ApprovalInstanceStatus.Rejected },
  { label: "已取消", value: ApprovalInstanceStatus.Canceled },
  { label: "已挂起", value: ApprovalInstanceStatus.Suspended },
  { label: "草稿", value: ApprovalInstanceStatus.Draft },
  { label: "已超时", value: ApprovalInstanceStatus.TimedOut },
  { label: "已终止", value: ApprovalInstanceStatus.Terminated },
];
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const drawerVisible = ref(false);
const instanceDetail = ref<ApprovalInstanceResponse | null>(null);
const taskList = ref<ApprovalTaskResponse[]>([]);
const historyList = ref<ApprovalHistoryEventResponse[]>([]);
const taskLoading = ref(false);
const businessData = ref<Array<{ field: string; value: string | null }>>([]);

const fetchData = async () => {
  loading.value = true;
  try {
    const statusValue = statusFilter.value === "all" ? undefined : statusFilter.value;
    const result = await getMyInstancesPaged({
      pageIndex: Number(pagination.current ?? 1),
      pageSize: Number(pagination.pageSize ?? 10)
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

const getStatusColor = (status: ApprovalInstanceStatus) => {
  switch (status) {
    case ApprovalInstanceStatus.Running:
      return "blue";
    case ApprovalInstanceStatus.Completed:
      return "green";
    case ApprovalInstanceStatus.Rejected:
      return "red";
    case ApprovalInstanceStatus.Canceled:
      return "default";
    case ApprovalInstanceStatus.Suspended:
      return "orange";
    case ApprovalInstanceStatus.Draft:
      return "purple";
    case ApprovalInstanceStatus.TimedOut:
      return "volcano";
    case ApprovalInstanceStatus.Terminated:
      return "magenta";
    case ApprovalInstanceStatus.AutoApproved:
      return "cyan";
    case ApprovalInstanceStatus.AutoRejected:
      return "geekblue";
    case ApprovalInstanceStatus.AiProcessing:
      return "processing";
    case ApprovalInstanceStatus.AiManualReview:
      return "gold";
    case ApprovalInstanceStatus.Destroy:
      return "default";
    default:
      return "default";
  }
};

const getStatusText = (status: ApprovalInstanceStatus) => {
  switch (status) {
    case ApprovalInstanceStatus.Running:
      return "运行中";
    case ApprovalInstanceStatus.Completed:
      return "已完成";
    case ApprovalInstanceStatus.Rejected:
      return "已驳回";
    case ApprovalInstanceStatus.Canceled:
      return "已取消";
    case ApprovalInstanceStatus.Suspended:
      return "已挂起";
    case ApprovalInstanceStatus.Draft:
      return "草稿";
    case ApprovalInstanceStatus.TimedOut:
      return "已超时";
    case ApprovalInstanceStatus.Terminated:
      return "已终止";
    case ApprovalInstanceStatus.AutoApproved:
      return "自动通过";
    case ApprovalInstanceStatus.AutoRejected:
      return "自动驳回";
    case ApprovalInstanceStatus.AiProcessing:
      return "AI审批中";
    case ApprovalInstanceStatus.AiManualReview:
      return "AI转人工";
    case ApprovalInstanceStatus.Destroy:
      return "已销毁";
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

const getTaskStatusColor = (status: ApprovalTaskStatus) => {
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

const getTaskStatusText = (status: ApprovalTaskStatus) => {
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

const handleViewDetail = async (id: string) => {
  drawerVisible.value = true;
  taskLoading.value = true;

  try {
    const [instance, tasks, history] = await Promise.all([
      getApprovalInstanceById(id),
      getApprovalTasksByInstance(id, { pageIndex: 1, pageSize: 100 }),
      getApprovalInstanceHistory(id, { pageIndex: 1, pageSize: 100 })
    ]);

    instanceDetail.value = instance;
    taskList.value = tasks.items;
    historyList.value = history.items;
    
    // 解析业务数据 DataJson
    businessData.value = [];
    if (instance.dataJson) {
      try {
        const parsed = JSON.parse(instance.dataJson);
        if (typeof parsed === "object" && parsed !== null) {
          businessData.value = Object.entries(parsed)
            .filter(([key]) => !key.startsWith("_")) // 排除内部字段
            .map(([field, value]) => ({
              field,
              value: value != null ? String(value) : null
            }));
        }
      } catch {
        // DataJson 解析失败时忽略
      }
    }

  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载详情失败");
  } finally {
    taskLoading.value = false;
  }
};

const handleCancel = async (id: string) => {
  try {
    await cancelApprovalInstance(id);
    message.success("取消成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "取消失败");
  }
};

const handleDrawerClose = () => {
  drawerVisible.value = false;
  instanceDetail.value = null;
  taskList.value = [];
  historyList.value = [];
  businessData.value = [];
};

const handleFilterUpdate = () => {
  emit('update-filter', { keyword: '', status: String(statusFilter.value) });
  fetchData();
};

onMounted(fetchData);

watch(statusFilter, () => {
  pagination.current = 1;
  fetchData();
});
</script>

<style scoped>
.tab-content-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}
/* Ensure the body of the card takes available space */
:deep(.ant-card-body) {
  flex: 1;
  overflow-y: auto;
}
</style>
