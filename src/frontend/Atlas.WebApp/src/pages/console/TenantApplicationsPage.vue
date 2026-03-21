<template>
  <div class="tenant-applications-page" data-testid="e2e-console-tenant-applications-page">
    <a-card :bordered="false" class="tenant-app-card">
      <template #title>租户开通关系</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedStatus"
            allow-clear
            placeholder="状态筛选"
            style="width: 140px"
            :options="statusOptions"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            placeholder="按目录名或 AppKey 检索"
            style="width: 260px"
            @search="handleSearch"
          />
          <a-button @click="resetFilters">重置</a-button>
        </a-space>
      </template>

      <a-table
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Active' ? 'processing' : 'default'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'openedAt'">
            {{ formatDate(record.openedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-button type="link" size="small" @click="viewDetail(record.id)">查看</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      title="租户开通关系详情"
      width="680"
      :destroy-on-close="true"
    >
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="ID">{{ detail?.id || "-" }}</a-descriptions-item>
        <a-descriptions-item label="目录ID">{{ detail?.applicationCatalogId || "-" }}</a-descriptions-item>
        <a-descriptions-item label="目录名称">{{ detail?.applicationCatalogName || "-" }}</a-descriptions-item>
        <a-descriptions-item label="应用实例ID">{{ detail?.tenantAppInstanceId || "-" }}</a-descriptions-item>
        <a-descriptions-item label="AppKey">{{ detail?.appKey || "-" }}</a-descriptions-item>
        <a-descriptions-item label="应用名称">{{ detail?.name || "-" }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ detail?.status || "-" }}</a-descriptions-item>
        <a-descriptions-item label="开通时间">{{ formatDate(detail?.openedAt) }}</a-descriptions-item>
        <a-descriptions-item label="更新时间">{{ formatDate(detail?.updatedAt) }}</a-descriptions-item>
        <a-descriptions-item label="数据源ID" :span="2">{{ detail?.dataSourceId || "-" }}</a-descriptions-item>
      </a-descriptions>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getTenantApplicationDetail, getTenantApplicationsPaged } from "@/services/api-tenant-applications";
import type { TenantApplicationDetail, TenantApplicationListItem } from "@/types/platform-v2";

const loading = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const rows = ref<TenantApplicationListItem[]>([]);
const detail = ref<TenantApplicationDetail | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);

const columns: TableColumnsType<TenantApplicationListItem> = [
  { title: "目录名", dataIndex: "applicationCatalogName", key: "applicationCatalogName", width: 180 },
  { title: "AppKey", dataIndex: "appKey", key: "appKey", width: 170 },
  { title: "应用名", dataIndex: "name", key: "name", width: 170 },
  { title: "开通时间", dataIndex: "openedAt", key: "openedAt", width: 180 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "操作", key: "actions", width: 100, fixed: "right" }
];

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => `共 ${all} 条`
});

const statusOptions = [
  { label: "Provisioning", value: "Provisioning" },
  { label: "Active", value: "Active" },
  { label: "Disabled", value: "Disabled" },
  { label: "Archived", value: "Archived" }
];

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

async function loadTenantApplications() {
  loading.value = true;
  try {
    const result  = await getTenantApplicationsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value
    });

    if (!isMounted.value) return;
    rows.value = result.items;
    pagination.value = {
      ...pagination.value,
      current: result.pageIndex,
      pageSize: result.pageSize,
      total: result.total
    };
  } catch (error) {
    message.error((error as Error).message || "加载租户开通关系失败");
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadTenantApplications();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  pageIndex.value = 1;
  void loadTenantApplications();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadTenantApplications();
}

async function viewDetail(id: string) {
  try {
    detail.value = await getTenantApplicationDetail(id);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载租户开通关系详情失败");
  }
}

onMounted(() => {
  void loadTenantApplications();
});
</script>

<style scoped>
.tenant-applications-page {
  padding: 24px;
}

.tenant-app-card {
  border-radius: 12px;
}
</style>
