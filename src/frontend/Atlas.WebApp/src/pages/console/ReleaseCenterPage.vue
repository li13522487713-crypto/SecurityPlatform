<template>
  <div class="release-center-page" data-testid="e2e-release-center-page">
    <a-card :bordered="false" class="release-card">
      <template #title>发布中心</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedStatus"
            allow-clear
            placeholder="状态筛选"
            style="width: 140px"
            :options="statusOptions"
          />
          <a-input
            v-model:value="appKeyFilter"
            allow-clear
            placeholder="按 AppKey 筛选"
            style="width: 180px"
          />
          <a-input
            v-model:value="manifestIdFilter"
            allow-clear
            placeholder="按目录ID筛选"
            style="width: 160px"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            placeholder="按发布说明检索"
            style="width: 240px"
            @search="handleSearch"
          />
          <a-button @click="resetFilters">重置</a-button>
        </a-space>
      </template>

      <a-table
        row-key="releaseId"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Released' ? 'processing' : 'default'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'releasedAt'">
            {{ formatDate(record.releasedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="viewDetail(record.releaseId)">详情</a-button>
              <a-button type="link" size="small" @click="openDebugTrace(record.releaseId)">审计追溯</a-button>
              <a-popconfirm title="确认回滚该发布版本？" @confirm="rollback(record.releaseId)">
                <a-button type="link" size="small" danger>回滚</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="detailVisible" title="发布详情" width="760px" :footer="null">
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="发布ID">{{ detail?.releaseId }}</a-descriptions-item>
        <a-descriptions-item label="目录ID">{{ detail?.applicationCatalogId }}</a-descriptions-item>
        <a-descriptions-item label="应用">{{ detail?.applicationCatalogName }}</a-descriptions-item>
        <a-descriptions-item label="AppKey">{{ detail?.appKey }}</a-descriptions-item>
        <a-descriptions-item label="版本">{{ detail?.version }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ detail?.status }}</a-descriptions-item>
        <a-descriptions-item label="发布时间" :span="2">{{ formatDate(detail?.releasedAt) }}</a-descriptions-item>
        <a-descriptions-item label="发布说明" :span="2">{{ detail?.releaseNote || "-" }}</a-descriptions-item>
      </a-descriptions>
      <a-typography-paragraph class="snapshot-title">SnapshotJson</a-typography-paragraph>
      <a-typography-paragraph class="snapshot-json">{{ detail?.snapshotJson || "{}" }}</a-typography-paragraph>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  getReleaseCenterDetail,
  getReleaseCenterPaged,
  rollbackReleaseCenter
} from "@/services/api-coze-runtime";
import type { ReleaseCenterDetail, ReleaseCenterListItem } from "@/types/platform-v2";

const route = useRoute();
const router = useRouter();
const loading = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const appKeyFilter = ref("");
const manifestIdFilter = ref("");
const rows = ref<ReleaseCenterListItem[]>([]);
const detail = ref<ReleaseCenterDetail | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);
const total = ref(0);

const columns: TableColumnsType<ReleaseCenterListItem> = [
  { title: "发布ID", dataIndex: "releaseId", key: "releaseId", width: 160 },
  { title: "应用目录", dataIndex: "applicationCatalogName", key: "applicationCatalogName", width: 180 },
  { title: "AppKey", dataIndex: "appKey", key: "appKey", width: 160 },
  { title: "版本", dataIndex: "version", key: "version", width: 90 },
  { title: "状态", dataIndex: "status", key: "status", width: 110 },
  { title: "发布时间", dataIndex: "releasedAt", key: "releasedAt", width: 180 },
  { title: "发布说明", dataIndex: "releaseNote", key: "releaseNote", ellipsis: true },
  { title: "操作", key: "actions", width: 220, fixed: "right" }
];

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => `共 ${all} 条`
});

const statusOptions = [
  { label: "Pending", value: "Pending" },
  { label: "Released", value: "Released" },
  { label: "RolledBack", value: "RolledBack" }
];

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString();
}

async function loadReleases() {
  loading.value = true;
  try {
    const parsedManifestId = manifestIdFilter.value.trim().length > 0
      ? Number.parseInt(manifestIdFilter.value.trim(), 10)
      : undefined;
    const result = await getReleaseCenterPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value,
      appKey: appKeyFilter.value || undefined,
      manifestId: Number.isNaN(parsedManifestId) ? undefined : parsedManifestId
    });
    rows.value = result.items;
    total.value = result.total;
    pagination.value = {
      ...pagination.value,
      current: result.pageIndex,
      pageSize: result.pageSize,
      total: result.total
    };
  } catch (error) {
    message.error((error as Error).message || "加载发布中心失败");
  } finally {
    loading.value = false;
  }
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadReleases();
}

function handleSearch() {
  pageIndex.value = 1;
  void loadReleases();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  appKeyFilter.value = "";
  manifestIdFilter.value = "";
  pageIndex.value = 1;
  void loadReleases();
}

async function viewDetail(releaseId: string) {
  try {
    detail.value = await getReleaseCenterDetail(releaseId);
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载发布详情失败");
  }
}

async function rollback(releaseId: string) {
  try {
    await rollbackReleaseCenter(releaseId);
    message.success("回滚任务已提交");
    await loadReleases();
  } catch (error) {
    message.error((error as Error).message || "回滚失败");
  }
}

function openDebugTrace(releaseId: string) {
  void router.push({
    path: "/console/debug",
    query: { releaseId }
  });
}

function readReleaseIdFromRouteQuery() {
  const releaseId = typeof route.query.releaseId === "string"
    ? route.query.releaseId.trim()
    : "";
  return releaseId;
}

function openReleaseDetailByRouteQuery() {
  const releaseId = readReleaseIdFromRouteQuery();
  if (!releaseId) {
    return;
  }
  void viewDetail(releaseId);
}

onMounted(() => {
  void loadReleases();
  openReleaseDetailByRouteQuery();
});

watch(
  () => route.query.releaseId,
  () => {
    openReleaseDetailByRouteQuery();
  }
);
</script>

<style scoped>
.release-center-page {
  padding: 24px;
}

.release-card {
  border-radius: 12px;
}

.snapshot-title {
  margin-top: 16px;
}

.snapshot-json {
  max-height: 220px;
  overflow: auto;
  white-space: pre-wrap;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  padding: 12px;
  border-radius: 8px;
}
</style>
