<template>
  <a-card :title="t('dynamicDesigner.modeEntity')" size="small">
    <template #extra>
      <a-space>
        <a-button @click="reload">{{ t("common.refresh") }}</a-button>
      </a-space>
    </template>

    <a-empty v-if="!tableKey" :description="t('dynamic.selectTableFirst')" />

    <template v-else>
      <a-descriptions :column="2" bordered size="small" style="margin-bottom: 12px">
        <a-descriptions-item :label="t('dynamic.selectedTableKey')">{{ tableKey }}</a-descriptions-item>
        <a-descriptions-item :label="t('common.status')">{{ summary?.status || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamic.selectedTableName')">{{ summary?.displayName || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamic.fieldsCount')">{{ summary?.fieldCount || 0 }}</a-descriptions-item>
      </a-descriptions>

      <a-table
        :data-source="fields"
        :columns="columns"
        row-key="name"
        :loading="loading"
        size="small"
        :pagination="{ pageSize: 10 }"
      />
    </template>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getDynamicTableFields, getDynamicTableSummary } from "@/services/api-dynamic-tables";
import type { DynamicFieldDefinition, DynamicTableSummary } from "@/types/dynamic-tables";

const route = useRoute();
const { t } = useI18n();

const loading = ref(false);
const summary = ref<DynamicTableSummary | null>(null);
const fields = ref<DynamicFieldDefinition[]>([]);

const tableKey = computed(() =>
  typeof route.query.tableKey === "string" ? route.query.tableKey.trim() : ""
);

const columns = computed(() => [
  { title: t("designer.entityModeling.fieldName"), dataIndex: "name", key: "name" },
  { title: t("designer.entityModeling.displayName"), dataIndex: "displayName", key: "displayName" },
  { title: t("designer.entityModeling.fieldType"), dataIndex: "fieldType", key: "fieldType" },
  {
    title: t("designer.entityModeling.allowNull"),
    dataIndex: "allowNull",
    key: "allowNull",
    customRender: ({ text }: { text: boolean }) => (text ? t("dynamic.yes") : t("dynamic.no")),
  },
]);

const reload = async () => {
  if (!tableKey.value) {
    summary.value = null;
    fields.value = [];
    return;
  }
  loading.value = true;
  try {
    const [nextSummary, nextFields] = await Promise.all([
      getDynamicTableSummary(tableKey.value),
      getDynamicTableFields(tableKey.value),
    ]);
    summary.value = nextSummary;
    fields.value = nextFields;
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadTableDetailFailed"));
  } finally {
    loading.value = false;
  }
};

watch(tableKey, () => {
  void reload();
}, { immediate: true });
</script>
