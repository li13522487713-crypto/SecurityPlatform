<template>
  <div style="padding: 24px;">
    <a-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
        <h3 style="margin: 0;">{{ t("approvalWorkspace.pageTitle") }}</h3>
      </div>

      <a-tabs v-model:active-key="activeTab" @change="handleTabChange">
        <a-tab-pane key="pending" :tab="t('approvalWorkspace.tabPending')">
          <a-table
            :columns="taskColumns"
            :data-source="pendingList"
            :loading="loading"
            :pagination="pagination"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <a-tag :color="taskStatusColor(record.status)">{{ taskStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatTime(record.createdAt) }}
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="done" :tab="t('approvalWorkspace.tabDone')">
          <a-table
            :columns="taskColumns"
            :data-source="doneList"
            :loading="loading"
            :pagination="pagination"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <a-tag :color="taskStatusColor(record.status)">{{ taskStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatTime(record.createdAt) }}
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="requests" :tab="t('approvalWorkspace.tabRequests')">
          <a-table
            :columns="instanceColumns"
            :data-source="requestsList"
            :loading="loading"
            :pagination="pagination"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <a-tag :color="instanceStatusColor(record.status)">{{ instanceStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatTime(record.createdAt) }}
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="cc" :tab="t('approvalWorkspace.tabCc')">
          <a-table
            :columns="ccColumns"
            :data-source="ccList"
            :loading="loading"
            :pagination="pagination"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'isRead'">
                <a-tag :color="record.isRead ? 'default' : 'blue'">
                  {{ record.isRead ? t("approvalWorkspace.readYes") : t("approvalWorkspace.readNo") }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatTime(record.createdAt) }}
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import { getMyTasksPaged, getMyInstancesPaged, getMyCopyRecordsPaged } from "@/services/api-approval";
import type { ApprovalTaskItem, ApprovalInstanceItem, ApprovalCopyRecord } from "@/services/api-approval";

const { t, locale } = useI18n();
const route = useRoute();

const activeTab = ref<string>("pending");
const loading = ref(false);
const pendingList = ref<ApprovalTaskItem[]>([]);
const doneList = ref<ApprovalTaskItem[]>([]);
const requestsList = ref<ApprovalInstanceItem[]>([]);
const ccList = ref<ApprovalCopyRecord[]>([]);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `${total}`
});

const taskColumns = computed(() => [
  { title: t("approvalWorkspace.colTitle"), dataIndex: "title", key: "title", ellipsis: true },
  { title: t("approvalWorkspace.colFlow"), dataIndex: "flowName", key: "flowName", width: 160 },
  { title: t("approvalWorkspace.colNode"), dataIndex: "currentNodeName", key: "currentNodeName", width: 140 },
  { title: t("approvalWorkspace.colStatus"), key: "status", width: 100 },
  { title: t("approvalWorkspace.colTime"), key: "createdAt", width: 180 }
]);

const instanceColumns = computed(() => [
  { title: t("approvalWorkspace.colTitle"), dataIndex: "title", key: "title", ellipsis: true },
  { title: t("approvalWorkspace.colFlow"), dataIndex: "flowName", key: "flowName", width: 160 },
  { title: t("approvalWorkspace.colStatus"), key: "status", width: 100 },
  { title: t("approvalWorkspace.colTime"), key: "createdAt", width: 180 }
]);

const ccColumns = computed(() => [
  { title: t("approvalWorkspace.colTitle"), dataIndex: "title", key: "title", ellipsis: true },
  { title: t("approvalWorkspace.colFlow"), dataIndex: "flowName", key: "flowName", width: 160 },
  { title: t("approvalWorkspace.colRead"), key: "isRead", width: 80 },
  { title: t("approvalWorkspace.colTime"), key: "createdAt", width: 180 }
]);

function taskStatusLabel(status: number) {
  const map: Record<number, string> = {
    0: t("approvalWorkspace.statusPending"),
    1: t("approvalWorkspace.statusApproved"),
    2: t("approvalWorkspace.statusRejected")
  };
  return map[status] ?? String(status);
}

function taskStatusColor(status: number) {
  const map: Record<number, string> = { 0: "processing", 1: "success", 2: "error" };
  return map[status] ?? "default";
}

function instanceStatusLabel(status: number) {
  const map: Record<number, string> = {
    0: t("approvalWorkspace.statusRunning"),
    1: t("approvalWorkspace.statusCompleted"),
    2: t("approvalWorkspace.statusRejected"),
    3: t("approvalWorkspace.statusCancelled")
  };
  return map[status] ?? String(status);
}

function instanceStatusColor(status: number) {
  const map: Record<number, string> = { 0: "processing", 1: "success", 2: "error", 3: "default" };
  return map[status] ?? "default";
}

function formatTime(iso: string) {
  if (!iso) return "-";
  const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleString(loc, { hour12: false });
}

async function loadData() {
  loading.value = true;
  const page = { pageIndex: pagination.current ?? 1, pageSize: pagination.pageSize ?? 10 };
  try {
    if (activeTab.value === "pending") {
      const result = await getMyTasksPaged(page, 0);
      pendingList.value = result.items as ApprovalTaskItem[];
      pagination.total = result.total;
    } else if (activeTab.value === "done") {
      const result = await getMyTasksPaged(page);
      doneList.value = result.items as ApprovalTaskItem[];
      pagination.total = result.total;
    } else if (activeTab.value === "requests") {
      const result = await getMyInstancesPaged(page);
      requestsList.value = result.items as ApprovalInstanceItem[];
      pagination.total = result.total;
    } else if (activeTab.value === "cc") {
      const result = await getMyCopyRecordsPaged(page);
      ccList.value = result.items as ApprovalCopyRecord[];
      pagination.total = result.total;
    }
  } catch {
    message.error(t("approvalWorkspace.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleTabChange() {
  pagination.current = 1;
  pagination.total = 0;
  void loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 10;
  void loadData();
}

onMounted(() => {
  const tab = route.query.tab as string;
  if (tab && ["pending", "done", "requests", "cc"].includes(tab)) {
    activeTab.value = tab;
  }
  void loadData();
});
</script>
