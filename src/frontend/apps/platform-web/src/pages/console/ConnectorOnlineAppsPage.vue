<template>
  <PageContainer title="在线应用">
    <a-table
      row-key="appKey"
      :data-source="rows"
      :columns="columns"
      :pagination="false"
      :loading="loading"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <StatusBadge :status="toBadgeStatus(record.status)" :text="record.status" />
        </template>
      </template>
    </a-table>
  </PageContainer>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { fetchConnectorOnlineApps, type ConnectorOnlineAppSummary } from "@atlas/connector-core";
import { PageContainer, StatusBadge } from "@atlas/shared-ui";
import { requestApi } from "@/services/api-core";

const rows = ref<ConnectorOnlineAppSummary[]>([]);
const loading = ref(false);

async function loadOnlineApps() {
  loading.value = true;
  try {
    rows.value = await fetchConnectorOnlineApps({ requestApi });
  } catch (error) {
    rows.value = [];
    message.error((error as Error).message || "加载在线应用失败");
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadOnlineApps();
});

const columns = computed<TableColumnsType<ConnectorOnlineAppSummary>>(() => [
  { title: "应用 Key", dataIndex: "appKey", key: "appKey" },
  { title: "应用名称", dataIndex: "appName", key: "appName" },
  { title: "状态", dataIndex: "status", key: "status" },
  { title: "能力数", dataIndex: "capabilityCount", key: "capabilityCount" },
  { title: "最近心跳", dataIndex: "lastHeartbeatAt", key: "lastHeartbeatAt" },
]);

function toBadgeStatus(status: ConnectorOnlineAppSummary["status"]) {
  if (status === "online") return "success";
  if (status === "degraded") return "warning";
  return "error";
}
</script>
