<template>
  <div class="runtime-contexts-page" data-testid="e2e-console-runtime-contexts-page">
    <a-card :bordered="false" class="runtime-context-card">
      <template #title>运行上下文</template>
      <template #extra>
        <a-space wrap>
          <a-input
            v-model:value="appKeyFilter"
            allow-clear
            placeholder="按 appKey 过滤"
            style="width: 180px"
          />
          <a-input
            v-model:value="pageKeyFilter"
            allow-clear
            placeholder="按 pageKey 过滤"
            style="width: 180px"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            placeholder="关键字检索"
            style="width: 200px"
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
          <template v-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'success' : 'default'">
              {{ record.isActive ? "是" : "否" }}
            </a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-button type="link" size="small" @click="viewDetail(record.appKey, record.pageKey)">查看</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      title="运行上下文详情"
      width="620"
      :destroy-on-close="true"
    >
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="ID">{{ detail?.id || "-" }}</a-descriptions-item>
        <a-descriptions-item label="AppKey">{{ detail?.appKey || "-" }}</a-descriptions-item>
        <a-descriptions-item label="PageKey">{{ detail?.pageKey || "-" }}</a-descriptions-item>
        <a-descriptions-item label="SchemaVersion">{{ detail?.schemaVersion ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="Environment">{{ detail?.environmentCode || "-" }}</a-descriptions-item>
        <a-descriptions-item label="IsActive">{{ detail?.isActive ? "是" : "否" }}</a-descriptions-item>
      </a-descriptions>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import { getRuntimeContextById, getRuntimeContextByRoute, getRuntimeContextsPaged } from "@/services/api-runtime-contexts";
import type { RuntimeContextDetail, RuntimeContextListItem } from "@/types/platform-v2";

const route = useRoute();
const loading = ref(false);
const keyword = ref("");
const appKeyFilter = ref("");
const pageKeyFilter = ref("");
const rows = ref<RuntimeContextListItem[]>([]);
const detail = ref<RuntimeContextDetail | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);

const columns: TableColumnsType<RuntimeContextListItem> = [
  { title: "AppKey", dataIndex: "appKey", key: "appKey", width: 180 },
  { title: "PageKey", dataIndex: "pageKey", key: "pageKey", width: 180 },
  { title: "SchemaVersion", dataIndex: "schemaVersion", key: "schemaVersion", width: 130 },
  { title: "Environment", dataIndex: "environmentCode", key: "environmentCode", width: 140 },
  { title: "IsActive", dataIndex: "isActive", key: "isActive", width: 110 },
  { title: "操作", key: "actions", width: 100, fixed: "right" }
];

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => `共 ${all} 条`
});

async function loadRuntimeContexts() {
  loading.value = true;
  try {
    const result  = await getRuntimeContextsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      appKey: appKeyFilter.value || undefined,
      pageKey: pageKeyFilter.value || undefined
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
    message.error((error as Error).message || "加载运行上下文失败");
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadRuntimeContexts();
}

function resetFilters() {
  keyword.value = "";
  appKeyFilter.value = "";
  pageKeyFilter.value = "";
  pageIndex.value = 1;
  void loadRuntimeContexts();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadRuntimeContexts();
}

async function viewDetail(appKey: string, pageKey: string) {
  try {
    detail.value = await getRuntimeContextByRoute(appKey, pageKey);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载运行上下文详情失败");
  }
}

function syncFiltersFromRouteQuery() {
  const queryAppKey = typeof route.query.appKey === "string" ? route.query.appKey.trim() : "";
  const queryPageKey = typeof route.query.pageKey === "string" ? route.query.pageKey.trim() : "";
  appKeyFilter.value = queryAppKey;
  pageKeyFilter.value = queryPageKey;
}

async function openDetailByRouteQuery() {
  const runtimeContextId = typeof route.query.runtimeContextId === "string"
    ? route.query.runtimeContextId.trim()
    : "";
  if (!runtimeContextId) {
    return;
  }

  try {
    detail.value = await getRuntimeContextById(runtimeContextId);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch {
    // ignore invalid runtimeContextId query to avoid noisy UX
  }
}

onMounted(() => {
  syncFiltersFromRouteQuery();
  void loadRuntimeContexts();
  void openDetailByRouteQuery();
});

watch(
  () => route.query,
  () => {
    syncFiltersFromRouteQuery();
    pageIndex.value = 1;
    void loadRuntimeContexts();
    void openDetailByRouteQuery();
  }
);
</script>

<style scoped>
.runtime-contexts-page {
  padding: 24px;
}

.runtime-context-card {
  border-radius: 12px;
}
</style>
