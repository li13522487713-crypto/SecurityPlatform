<template>
  <a-card :title="pageTitle" class="page-card">
    <template #extra>
      <a-button type="primary" ghost @click="openFieldDesign">设计字段</a-button>
    </template>
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" :data="pageData" />
      <a-empty v-else-if="!loading" :description="t('dynamic.emptyNoPage')" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getDynamicAmisSchema, getDynamicTableDetail } from "@/services/dynamic-tables";
import type { AmisSchema } from "@/types/amis";

const route = useRoute();
const router = useRouter();
const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref(t("dynamic.crudTitle"));
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

  // 记录当前请求的 tableKey，防止旧请求覆盖新状态
  const currentTableKey = tableKey.value;
  loading.value = true;
  try {
    const schemaResult = await getDynamicAmisSchema(`${currentTableKey}/crud`);
    if (!isMounted.value || currentTableKey !== tableKey.value) return;
    schema.value = schemaResult as AmisSchema;
    
    // 非阻断式加载详情
    getDynamicTableDetail(currentTableKey).then(detail => {
      if (!isMounted.value || currentTableKey !== tableKey.value) return;
      tableDisplayName.value = detail?.displayName ?? currentTableKey;
      pageTitle.value = detail?.displayName ?? t("dynamic.crudTitle");
      approvalFlowDefinitionId.value = detail?.approvalFlowDefinitionId ?? null;
    }).catch(err => {
      console.warn("Failed to load table detail:", err);
      if (!isMounted.value || currentTableKey !== tableKey.value) return;
      tableDisplayName.value = currentTableKey;
      pageTitle.value = currentTableKey;
    });

  } catch (error) {
    if (!isMounted.value || currentTableKey !== tableKey.value) return;
    schema.value = null;
    message.error((error as Error).message || t("dynamic.loadPageFailed", "动态表页面加载失败"));
  } finally {
    if (isMounted.value && currentTableKey === tableKey.value) {
      loading.value = false;
    }
  }
};

const openFieldDesign = () => {
  const appId = typeof route.params.appId === "string" ? route.params.appId : "";
  if (!appId || !tableKey.value) {
    return;
  }

  void router.push(`/apps/${encodeURIComponent(appId)}/data/${encodeURIComponent(tableKey.value)}/design`);
};

onMounted(() => {
  loadSchema();
});

watch(tableKey, () => {
  loadSchema();
});
</script>
