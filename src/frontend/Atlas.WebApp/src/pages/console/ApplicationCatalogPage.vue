<template>
  <div class="application-catalog-page" data-testid="e2e-console-application-catalog-page">
    <a-card :bordered="false" class="catalog-card">
      <template #title>应用目录</template>
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
            v-model:value="categoryFilter"
            allow-clear
            placeholder="分类筛选"
            style="width: 160px"
          />
          <a-input
            v-model:value="appKeyFilter"
            allow-clear
            placeholder="按 AppKey 筛选"
            style="width: 180px"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            placeholder="按名称或 CatalogKey 检索"
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
            <a-tag :color="record.status === 'Published' ? 'processing' : 'default'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'publishedAt'">
            {{ formatDate(record.publishedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-button type="link" size="small" @click="viewDetail(record.id)">查看</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      title="应用目录详情"
      width="640"
      :destroy-on-close="true"
    >
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="ID">{{ detail?.id || "-" }}</a-descriptions-item>
        <a-descriptions-item label="CatalogKey">{{ detail?.catalogKey || "-" }}</a-descriptions-item>
        <a-descriptions-item label="名称">{{ detail?.name || "-" }}</a-descriptions-item>
        <a-descriptions-item label="版本">{{ detail?.version ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ detail?.status || "-" }}</a-descriptions-item>
        <a-descriptions-item label="分类">{{ detail?.category || "-" }}</a-descriptions-item>
        <a-descriptions-item label="发布时间" :span="2">
          {{ formatDate(detail?.publishedAt) }}
        </a-descriptions-item>
        <a-descriptions-item label="数据源ID" :span="2">
          {{ detail?.dataSourceId || "-" }}
        </a-descriptions-item>
        <a-descriptions-item label="描述" :span="2">
          {{ detail?.description || "-" }}
        </a-descriptions-item>
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
import {
  getApplicationCatalogDetail,
  getApplicationCatalogsPaged
} from "@/services/api-application-catalogs";
import type { ApplicationCatalogDetail, ApplicationCatalogListItem } from "@/types/platform-v2";

const loading = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const categoryFilter = ref("");
const appKeyFilter = ref("");
const rows = ref<ApplicationCatalogListItem[]>([]);
const detail = ref<ApplicationCatalogDetail | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);

const columns: TableColumnsType<ApplicationCatalogListItem> = [
  { title: "名称", dataIndex: "name", key: "name", width: 180 },
  { title: "CatalogKey", dataIndex: "catalogKey", key: "catalogKey", width: 180 },
  { title: "版本", dataIndex: "version", key: "version", width: 100 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "分类", dataIndex: "category", key: "category", width: 140 },
  { title: "发布时间", dataIndex: "publishedAt", key: "publishedAt", width: 180 },
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
  { label: "Draft", value: "Draft" },
  { label: "Published", value: "Published" },
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

async function loadCatalogs() {
  loading.value = true;
  try {
    const result  = await getApplicationCatalogsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value,
      category: categoryFilter.value || undefined,
      appKey: appKeyFilter.value || undefined
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
    message.error((error as Error).message || "加载应用目录失败");
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadCatalogs();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  categoryFilter.value = "";
  appKeyFilter.value = "";
  pageIndex.value = 1;
  void loadCatalogs();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadCatalogs();
}

async function viewDetail(id: string) {
  try {
    detail.value = await getApplicationCatalogDetail(id);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载应用目录详情失败");
  }
}

onMounted(() => {
  void loadCatalogs();
});
</script>

<style scoped>
.application-catalog-page {
  padding: 24px;
}

.catalog-card {
  border-radius: 12px;
}
</style>
