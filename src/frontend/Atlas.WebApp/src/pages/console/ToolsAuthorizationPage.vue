<template>
  <a-card title="Tools Authorization Center">
    <a-space direction="vertical" style="width: 100%">
      <a-alert type="info" show-icon message="产品化重构 V1：工具授权策略矩阵与策略模拟入口" />
      <a-table
        :data-source="items"
        :columns="columns"
        :loading="loading"
        row-key="id"
        :pagination="{ current: pageIndex, pageSize, total, showSizeChanger: false }"
        @change="handlePageChange"
      />
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getToolAuthorizationPolicies } from "@/services/api-productization";

interface PolicyItem {
  id: string;
  toolId: string;
  toolName: string;
  policyType: string;
  rateLimitQuota: number;
  auditEnabled: boolean;
}

const loading = ref(false);
const items = ref<PolicyItem[]>([]);
const pageIndex = ref(1);
const pageSize = ref(10);
const total = ref(0);

const columns: TableColumnsType<PolicyItem> = [
  { title: "Tool ID", dataIndex: "toolId", key: "toolId" },
  { title: "Tool Name", dataIndex: "toolName", key: "toolName" },
  { title: "Policy", dataIndex: "policyType", key: "policyType" },
  { title: "Quota", dataIndex: "rateLimitQuota", key: "rateLimitQuota" },
  {
    title: "Audit",
    dataIndex: "auditEnabled",
    key: "auditEnabled",
    customRender: ({ text }) => (text ? "Enabled" : "Disabled"),
  },
];

async function load() {
  loading.value = true;
  try {
    const result  = await getToolAuthorizationPolicies(pageIndex.value, pageSize.value);

    if (!isMounted.value) return;
    items.value = result.items;
    total.value = result.total;
  } catch (error) {
    message.error((error as Error).message || "加载策略失败");
  } finally {
    loading.value = false;
  }
}

function handlePageChange(pagination: { current?: number; pageSize?: number }) {
  pageIndex.value = pagination.current ?? 1;
  pageSize.value = pagination.pageSize ?? 10;
  load();
}

onMounted(load);
</script>
