<template>
  <div class="approval-tasks-page">
    <!-- 顶部工具栏 -->
    <FilterToolbar
      v-model:keyword="keyword"
      :show-search="true"
      search-placeholder="按标题或节点关键词检索"
      :search-width="240"
      :show-refresh="true"
      @search="handleFilterUpdate"
      @refresh="fetchData"
    >
      <a-select v-model:value="statusFilter" style="width: 140px" :options="statusOptions" @change="handleFilterUpdate" />
      <a-select
        v-model:value="selectedFlowId"
        style="width: 200px"
        :loading="flowLoading"
        :options="flowOptions"
        allow-clear
        show-search
        placeholder="按流程类型过滤"
        @change="handleFlowFilterChange"
      />
      <a-select
        v-model:value="selectedAppId"
        style="width: 200px"
        :loading="appLoading"
        :options="appOptions"
        allow-clear
        show-search
        placeholder="按应用过滤"
        @change="handleAppScopeChange"
      />
    </FilterToolbar>

    <!-- 主从布局容器 -->
    <MasterDetailLayout :detail-visible="isDetailVisible" :master-width="380">
      <template #master>
        <a-spin :spinning="loading">
          <div class="task-list">
            <template v-if="dataSource.length > 0">
              <div 
                v-for="item in dataSource" 
                :key="item.id"
                class="task-card"
                :class="{ 'is-active': selectedItem?.id === item.id }"
                @click="selectItem(item)"
              >
                <div class="task-card-header">
                  <span class="task-flow">{{ item.flowName }}</span>
                  <ApprovalStatusTag :status="item.status" />
                </div>
                <div class="task-card-title">{{ item.title }}</div>
                <div class="task-card-meta">
                  <span>当前节点: {{ item.currentNodeName }}</span>
                  <SlaIndicator :remaining-minutes="item.slaRemainingMinutes" />
                </div>
                <div class="task-card-time">{{ formatTime(item.createdAt) }}</div>
              </div>
            </template>
            <a-empty v-else description="暂无审批待办" style="margin-top: 60px;" />
          </div>
          
          <div class="pagination-wrapper" v-if="pagination.total && pagination.total > 0">
            <a-pagination
              v-model:current="pagination.current"
              :total="pagination.total"
              :pageSize="pagination.pageSize"
              size="small"
              @change="onPageChange"
            />
          </div>
        </a-spin>
      </template>

      <!-- 右侧详情面板 (Detail) -->
      <template #detail>
        <ApprovalTaskDetailPanel
          v-if="selectedItem"
          :task-id="selectedItem.id"
          @close="clearSelection"
          @refresh="fetchDataAndRetainSelection"
        />
      </template>
    </MasterDetailLayout>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { getMyTasksPaged } from "@/services/api";
import { getApprovalFlowsPaged } from "@/services/api-approval";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import type { TablePaginationConfig } from "ant-design-vue";
import { ApprovalTaskStatus, type ApprovalTaskResponse } from "@/types/api";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import { useMasterDetail } from "@/composables/useMasterDetail";
import MasterDetailLayout from "@/components/layout/MasterDetailLayout.vue";
import FilterToolbar from "@/components/common/FilterToolbar.vue";
import ApprovalTaskDetailPanel from "@/components/approval/ApprovalTaskDetailPanel.vue";
import ApprovalStatusTag from "@/components/approval/ApprovalStatusTag.vue";
import SlaIndicator from "@/components/approval/SlaIndicator.vue";

const props = defineProps<{
  urlKeyword?: string;
  urlStatus?: string;
}>();
const route = useRoute();

const emit = defineEmits<{
  'update-filter': [{keyword: string, status: string}];
}>();

const dataSource = ref<ApprovalTaskResponse[]>([]);
const loading = ref(false);
const appLoading = ref(false);
const keyword = ref(props.urlKeyword || "");
const statusFilter = ref<ApprovalTaskStatus | "all">((props.urlStatus as unknown as ApprovalTaskStatus) || "all");
const selectedAppId = ref<string | undefined>(getCurrentAppIdFromStorage() ?? undefined);
const appOptions = ref<Array<{ label: string; value: string }>>([]);
const selectedFlowId = ref<string | undefined>(undefined);
const flowLoading = ref(false);
const flowOptions = ref<Array<{ label: string; value: string }>>([]);
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
  total: 0
});

const { selectedItem, isDetailVisible, selectItem, clearSelection } = useMasterDetail<ApprovalTaskResponse>();

const fetchData = async () => {
  loading.value = true;
  try {
    const statusValue = statusFilter.value === "all" ? undefined : statusFilter.value;
    const result = await getMyTasksPaged({
      pageIndex: Number(pagination.current ?? 1),
      pageSize: Number(pagination.pageSize ?? 10),
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

const applyDeepLinkFocus = () => {
  const urlTaskId = typeof route.query.taskId === "string" ? route.query.taskId : "";
  if (!urlTaskId) return;
  const matched = dataSource.value.find((item) => String(item.id) === urlTaskId);
  if (matched) {
    selectItem(matched);
  }
};

const fetchDataAndRetainSelection = async () => {
  await fetchData();
  if (selectedItem.value) {
    const stillExists = dataSource.value.find(t => t.id === selectedItem.value!.id);
    if (!stillExists) clearSelection();
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

const loadFlowOptions = async () => {
  flowLoading.value = true;
  try {
    const result = await getApprovalFlowsPaged({ pageIndex: 1, pageSize: 200 });
    flowOptions.value = result.items.map((item) => ({
      label: item.name,
      value: String(item.id),
    }));
  } catch {
    // 静默失败，流程过滤非关键功能
  } finally {
    flowLoading.value = false;
  }
};

const handleFlowFilterChange = () => {
  pagination.current = 1;
  clearSelection();
  fetchData();
};

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value);
  pagination.current = 1;
  clearSelection();
  fetchData();
};

const handleFilterUpdate = () => {
  emit('update-filter', { keyword: keyword.value, status: String(statusFilter.value) });
  fetchData();
};

const onPageChange = (page: number) => {
  pagination.current = Number(page);
  clearSelection();
  fetchData();
};

const formatTime = (value: string) => {
  return new Date(value).toLocaleString([], { month: '2-digit', day: '2-digit', hour: '2-digit', minute:'2-digit' });
};

onMounted(async () => {
  await Promise.all([loadAppOptions(), loadFlowOptions()]);
  await fetchData();
  applyDeepLinkFocus();
});

watch(statusFilter, () => {
  pagination.current = 1;
  clearSelection();
  fetchData();
});
</script>

<style scoped>
.approval-tasks-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0;
  background: var(--color-bg-base);
}

/* Card List Styles */
.task-list {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.task-card {
  background: #fff;
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s;
}

.task-card:hover {
  border-color: var(--color-primary);
  box-shadow: var(--shadow-sm);
}

.task-card.is-active {
  background: var(--color-primary-bg);
  border-color: var(--color-primary);
}

.task-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.task-flow {
  font-size: 13px;
  color: var(--color-text-tertiary);
}

.task-card-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-card-meta {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
}

.sla-ok { color: var(--color-success); }
.sla-error { color: var(--color-error-text); }

.task-card-time {
  font-size: 12px;
  color: var(--color-text-quaternary);
}

.pagination-wrapper {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  background: var(--color-bg-container);
  display: flex;
  justify-content: center;
}

/* Response handling for mobile */
@media screen and (max-width: 768px) {
  .master-detail-container {
    position: relative;
  }
  .has-detail .master-list {
    display: none; /* Hide list on mobile when detail is shown */
  }
}
</style>
