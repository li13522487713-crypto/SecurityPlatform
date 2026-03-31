<template>
  <div class="field-mapping-panel">
    <a-table
      size="small"
      :pagination="false"
      :data-source="mappings"
      :columns="columns"
      row-key="targetFieldKey"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { OutputFieldMapping } from "@/types/dynamic-dataflow";

const props = defineProps<{
  mappings: OutputFieldMapping[];
}>();

const { t } = useI18n();

const columns = computed(() => [
  { title: t("dynamicDesigner.targetField"), dataIndex: "targetFieldKey", key: "targetFieldKey" },
  {
    title: t("dynamicDesigner.sourceField"),
    key: "source",
    customRender: ({ record }: { record: OutputFieldMapping }) => record.source?.fieldKey ?? "-"
  },
  { title: t("dynamicDesigner.type"), dataIndex: "targetType", key: "targetType" },
  {
    title: t("dynamicDesigner.pipeline"),
    key: "pipeline",
    customRender: ({ record }: { record: OutputFieldMapping }) =>
      record.pipeline.map(op => op.type).join(" -> ") || "-"
  }
]);
</script>

<style scoped>
.field-mapping-panel {
  padding: 8px;
}
</style>
