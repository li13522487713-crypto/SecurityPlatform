<template>
  <a-card :title="pageTitle" class="page-card">
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" :data="pageData" />
      <a-empty v-else-if="!loading" description="未找到页面配置" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getDynamicAmisSchema, getDynamicTableDetail } from "@/services/dynamic-tables";
import type { JsonValue } from "@/types/api";

const route = useRoute();
const loading = ref(false);
const schema = ref<JsonValue | null>(null);
const pageTitle = ref("动态数据管理");
const tableDisplayName = ref<string | null>(null);

const tableKey = computed(() => {
  const key = route.params.tableKey;
  return typeof key === "string" ? key : "";
});

const approvalFlowDefinitionId = ref<number | null>(null);

const pageData = computed(() => ({
  tableKey: tableKey.value,
  tableDisplayName: tableDisplayName.value ?? tableKey.value,
  approvalFlowDefinitionId: approvalFlowDefinitionId.value
}));

const loadSchema = async () => {
  if (!tableKey.value) {
    schema.value = null;
    return;
  }

  loading.value = true;
  try {
    const [schemaResult, detail] = await Promise.all([
      getDynamicAmisSchema(`${tableKey.value}/crud`),
      getDynamicTableDetail(tableKey.value)
    ]);
    schema.value = schemaResult;
    tableDisplayName.value = detail?.displayName ?? tableKey.value;
    pageTitle.value = detail?.displayName ?? "动态数据管理";
    approvalFlowDefinitionId.value = detail?.approvalFlowDefinitionId ?? null;
  } catch (error) {
    schema.value = null;
    message.error((error as Error).message || "加载页面失败");
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadSchema();
});

watch(tableKey, () => {
  loadSchema();
});
</script>
