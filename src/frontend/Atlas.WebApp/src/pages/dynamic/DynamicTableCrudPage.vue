<template>
  <a-card :title="pageTitle" class="page-card">
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

import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getDynamicAmisSchema, getDynamicTableDetail } from "@/services/dynamic-tables";
import type { AmisSchema } from "@/types/amis";

const route = useRoute();
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

  loading.value = true;
  try {
    const [schemaResult, detail]  = await Promise.all([
      getDynamicAmisSchema(`${tableKey.value}/crud`),
      getDynamicTableDetail(tableKey.value)
    ]);
    if (!isMounted.value) return;
    schema.value = schemaResult as AmisSchema;
    tableDisplayName.value = detail?.displayName ?? tableKey.value;
    pageTitle.value = detail?.displayName ?? t("dynamic.crudTitle");
    approvalFlowDefinitionId.value = detail?.approvalFlowDefinitionId ?? null;
  } catch (error) {
    schema.value = null;
    message.error((error as Error).message || t("dynamic.loadPageFailed"));
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
