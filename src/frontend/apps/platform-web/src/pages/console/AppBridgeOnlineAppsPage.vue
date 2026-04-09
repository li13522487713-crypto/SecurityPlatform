<template>
  <section class="appbridge-page">
    <h2>在线应用中心</h2>
    <a-table
      row-key="appInstanceId"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="pagination"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button size="small" @click="goExposure(record.appInstanceId)">暴露策略</a-button>
            <a-button size="small" @click="goDataBrowser(record.appInstanceId)">数据浏览</a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import type { TableColumnsType } from "ant-design-vue";
import type { PagedRequest } from "@atlas/shared-core";
import type { OnlineAppProjectionItem } from "@/types/platform-console";
import { getOnlineApps } from "@/services/api-appbridge";

const router = useRouter();
const loading = ref(false);
const rows = ref<OnlineAppProjectionItem[]>([]);
const total = ref(0);
const request = ref<PagedRequest>({
  pageIndex: 1,
  pageSize: 20,
  keyword: "",
  sortBy: "",
  sortDesc: false
});

const columns = computed<TableColumnsType<OnlineAppProjectionItem>>(() => [
  { title: "实例ID", dataIndex: "appInstanceId", key: "appInstanceId", width: 140 },
  { title: "应用Key", dataIndex: "appKey", key: "appKey", width: 160 },
  { title: "应用名称", dataIndex: "appName", key: "appName", width: 200 },
  { title: "桥接模式", dataIndex: "bridgeMode", key: "bridgeMode", width: 120 },
  { title: "运行状态", dataIndex: "runtimeStatus", key: "runtimeStatus", width: 120 },
  { title: "健康状态", dataIndex: "healthStatus", key: "healthStatus", width: 120 },
  { title: "版本", dataIndex: "releaseVersion", key: "releaseVersion", width: 120 },
  { title: "最近心跳", dataIndex: "lastSeenAt", key: "lastSeenAt", width: 220 },
  { title: "操作", key: "actions", fixed: "right", width: 220 }
]);

const pagination = computed(() => ({
  current: request.value.pageIndex,
  pageSize: request.value.pageSize,
  total: total.value,
  showSizeChanger: true
}));

async function load() {
  loading.value = true;
  try {
    const result = await getOnlineApps(request.value);
    rows.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function onTableChange(page: { current?: number; pageSize?: number }) {
  request.value = {
    ...request.value,
    pageIndex: page.current ?? 1,
    pageSize: page.pageSize ?? 20
  };
  void load();
}

function goExposure(appInstanceId: string) {
  void router.push(`/console/appbridge/apps/${appInstanceId}/exposure-policy`);
}

function goDataBrowser(appInstanceId: string) {
  void router.push(`/console/appbridge/data-browser?appInstanceId=${encodeURIComponent(appInstanceId)}`);
}

onMounted(() => {
  void load();
});
</script>

<style scoped>
.appbridge-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
