<template>
  <a-card title="指令下发面板">
    <a-empty v-if="rows.length === 0" description="暂无指令记录" />
    <a-table
      v-else
      row-key="id"
      :data-source="rows"
      :columns="columns"
      :pagination="false"
      size="small"
    />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { type ConnectorCommandLogEntry } from "../services/connector-api";

interface Props {
  logs?: ConnectorCommandLogEntry[];
}

const props = withDefaults(defineProps<Props>(), {
  logs: () => []
});

const rows = computed(() => props.logs ?? []);

const columns = computed<TableColumnsType<ConnectorCommandLogEntry>>(() => [
  { title: "命令 ID", dataIndex: "id", key: "id" },
  { title: "应用", dataIndex: "appKey", key: "appKey" },
  { title: "命令", dataIndex: "commandType", key: "commandType" },
  { title: "状态", dataIndex: "status", key: "status" },
  { title: "开始时间", dataIndex: "createdAt", key: "createdAt" },
  { title: "结束时间", dataIndex: "finishedAt", key: "finishedAt" },
  { title: "说明", dataIndex: "message", key: "message" }
]);
</script>
