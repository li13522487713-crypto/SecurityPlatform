<template>
  <div class="approval-done-page">
    <FilterToolbar
      v-model:keyword="keyword"
      :show-search="true"
      search-placeholder="按任务标题检索"
      :search-width="240"
      :show-refresh="true"
      @search="handleFilterUpdate"
      @refresh="fetchData"
    >
      <a-select v-model:value="statusFilter" style="width: 140px" :options="statusOptions" @change="handleFilterUpdate" />
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
                  <a-tag :color="getStatusColor(item.status)">{{ getStatusText(item.status) }}</a-tag>
                </div>
                <div class="task-card-title">{{ item.title }}</div>
                <div class="task-card-meta">
                  <span>当前节点: {{ item.currentNodeName }}</span>
                </div>
                <div class="task-card-time">处理时间: {{ formatTime(item.decisionAt) }}</div>
              </div>
            </template>
            <a-empty v-else description="暂无已办任务" style="margin-top: 60px;" />
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
import { onMounted, reactive, ref, watch, onUnmounted } from 'vue';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { ApprovalTaskStatus, type ApprovalTaskResponse } from '@/types/api';
import { getMyTasksPaged } from '@/services/api';
import { useMasterDetail } from '@/composables/useMasterDetail';
import MasterDetailLayout from '@/components/layout/MasterDetailLayout.vue';
import FilterToolbar from '@/components/common/FilterToolbar.vue';
import ApprovalTaskDetailPanel from '@/components/approval/ApprovalTaskDetailPanel.vue';

const props = defineProps<{
  urlKeyword?: string;
  urlStatus?: string;
}>();

const emit = defineEmits<{
  'update-filter': [{keyword: string, status: string}];
}>();

const dataSource = ref<ApprovalTaskResponse[]>([]);
const loading = ref(false);
const keyword = ref(props.urlKeyword || '');
const statusFilter = ref<ApprovalTaskStatus | 'all'>((props.urlStatus as unknown as ApprovalTaskStatus) || 'all');
const doneStatusSet = new Set<ApprovalTaskStatus>([
  ApprovalTaskStatus.Approved,
  ApprovalTaskStatus.Rejected,
  ApprovalTaskStatus.Canceled,
  ApprovalTaskStatus.Delegated
]);
const statusOptions = [
  { label: '全部', value: 'all' },
  { label: '已同意', value: ApprovalTaskStatus.Approved },
  { label: '已驳回', value: ApprovalTaskStatus.Rejected },
  { label: '已取消', value: ApprovalTaskStatus.Canceled },
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
    const statusValue = statusFilter.value === 'all' ? undefined : statusFilter.value;
    const result  = await getMyTasksPaged(
      {
        pageIndex: Number(pagination.current ?? 1),
        pageSize: Number(pagination.pageSize ?? 10),
        keyword: keyword.value || undefined,
      },
      statusValue,
    );

    if (!isMounted.value) return;
    const items = statusValue === undefined
      ? result.items.filter((item) => doneStatusSet.has(item.status))
      : result.items;
    dataSource.value = items;
    pagination.total = statusValue === undefined ? items.length : result.total; // Approximated total for 'all'
  } catch (err) {
    message.error(err instanceof Error ? err.message : '查询失败');
  } finally {
    loading.value = false;
  }
};

const fetchDataAndRetainSelection = async () => {
  await fetchData();

  if (!isMounted.value) return;
  if (selectedItem.value) {
    const stillExists = dataSource.value.find(t => t.id === selectedItem.value!.id);
    if (!stillExists) clearSelection();
  }
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

const getStatusColor = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Approved: return 'success';
    case ApprovalTaskStatus.Rejected: return 'error';
    case ApprovalTaskStatus.Canceled: return 'default';
    case ApprovalTaskStatus.Delegated: return 'purple';
    default: return 'default';
  }
};

const getStatusText = (status: ApprovalTaskStatus) => {
  switch (status) {
    case ApprovalTaskStatus.Approved: return '已同意';
    case ApprovalTaskStatus.Rejected: return '已驳回';
    case ApprovalTaskStatus.Canceled: return '已取消';
    case ApprovalTaskStatus.Delegated: return '已委派';
    default: return '处理中';
  }
};

const formatTime = (value?: string) => {
  if (!value) return '-';
  return new Date(value).toLocaleString([], { month: '2-digit', day: '2-digit', hour: '2-digit', minute:'2-digit' });
};

onMounted(() => {
  void fetchData();
});

watch(statusFilter, () => {
  pagination.current = 1;
  clearSelection();
  fetchData();
});
</script>

<style scoped>
.approval-done-page {
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

</style>
