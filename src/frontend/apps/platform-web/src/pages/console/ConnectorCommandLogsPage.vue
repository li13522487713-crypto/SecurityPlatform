<template>
  <PageContainer title="指令日志">
    <a-space style="margin-bottom: 12px;">
      <a-button size="small" @click="refresh">刷新</a-button>
    </a-space>
    <a-table
      row-key="id"
      :data-source="rows"
      :columns="columns"
      :pagination="false"
      size="small"
    />
  </PageContainer>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { getConnectorCommandHistory, type ConnectorCommandLogEntry } from "@atlas/connector-core";
import { PageContainer } from "@atlas/shared-ui";

const rows = ref<ConnectorCommandLogEntry[]>([]);

function refresh() {
  rows.value = getConnectorCommandHistory();
}

const columns = computed<TableColumnsType<ConnectorCommandLogEntry>>(() => [
  { title: "命令 ID", dataIndex: "id", key: "id" },
  { title: "应用", dataIndex: "appKey", key: "appKey" },
  { title: "命令", dataIndex: "commandType", key: "commandType" },
  { title: "状态", dataIndex: "status", key: "status" },
  { title: "开始时间", dataIndex: "createdAt", key: "createdAt" },
  { title: "结束时间", dataIndex: "finishedAt", key: "finishedAt" },
  { title: "说明", dataIndex: "message", key: "message" },
]);

onMounted(() => {
  refresh();
});
</script>
