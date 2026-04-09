<template>
  <a-card title="连接器配置表单">
    <a-table
      row-key="id"
      :data-source="rows"
      :columns="columns"
      :loading="loading"
      :pagination="false"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag :color="record.isActive ? 'green' : 'default'">
            {{ record.isActive ? "启用" : "停用" }}
          </a-tag>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { type ConnectorRecord } from "../services/connector-api";

interface Props {
  items?: ConnectorRecord[];
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  items: () => [],
  loading: false
});

const rows = computed(() => props.items ?? []);

const columns = computed<TableColumnsType<ConnectorRecord>>(() => [
  { title: "ID", dataIndex: "id", key: "id" },
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "基地址", dataIndex: "baseUrl", key: "baseUrl" },
  { title: "认证类型", dataIndex: "authType", key: "authType" },
  { title: "超时(秒)", dataIndex: "timeoutSeconds", key: "timeoutSeconds" },
  { title: "是否启用", dataIndex: "isActive", key: "isActive" }
]);
</script>
