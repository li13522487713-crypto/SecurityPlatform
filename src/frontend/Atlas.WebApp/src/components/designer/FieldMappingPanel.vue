<template>
  <div class="field-mapping-panel">
    <div class="toolbar">
      <a-button type="dashed" size="small" @click="addRow">{{ t("common.create", "新增") }}</a-button>
    </div>
    <a-table
      size="small"
      :pagination="false"
      :data-source="localMappings"
      :columns="columns"
      row-key="targetFieldKey"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'targetFieldKey'">
          <a-input
            :value="record.targetFieldKey"
            size="small"
            @change="onText(index, 'targetFieldKey', ($event.target as HTMLInputElement).value)"
          />
        </template>
        <template v-else-if="column.key === 'targetLabel'">
          <a-input
            :value="record.targetLabel"
            size="small"
            @change="onText(index, 'targetLabel', ($event.target as HTMLInputElement).value)"
          />
        </template>
        <template v-else-if="column.key === 'source'">
          <a-input
            :value="record.source?.fieldKey ?? ''"
            size="small"
            :placeholder="t('dynamicDesigner.sourceField')"
            @change="onSourceField(index, ($event.target as HTMLInputElement).value)"
          />
        </template>
        <template v-else-if="column.key === 'type'">
          <a-select
            :value="record.targetType"
            size="small"
            style="width: 110px"
            @change="onText(index, 'targetType', String($event))"
          >
            <a-select-option v-for="tpe in fieldTypes" :key="tpe" :value="tpe">{{ tpe }}</a-select-option>
          </a-select>
        </template>
        <template v-else-if="column.key === 'nullable'">
          <a-switch
            :checked="record.nullable ?? true"
            size="small"
            @change="onNullable(index, Boolean($event))"
          />
        </template>
        <template v-else-if="column.key === 'onError'">
          <a-select
            :value="record.onError ?? 'null'"
            size="small"
            style="width: 110px"
            @change="onText(index, 'onError', String($event))"
          >
            <a-select-option value="null">null</a-select-option>
            <a-select-option value="default">default</a-select-option>
            <a-select-option value="reject_row">reject_row</a-select-option>
          </a-select>
        </template>
        <template v-else-if="column.key === 'pipeline'">
          <div class="pipeline-cell">
            <a-select
              :value="record.pipeline[0]?.type ?? undefined"
              size="small"
              style="width: 100px"
              :placeholder="t('dynamicDesigner.pipeline')"
              @change="onPipelineType(index, String($event) as TransformOp['type'])"
            >
              <a-select-option v-for="op in pipelineOps" :key="op" :value="op">{{ op }}</a-select-option>
            </a-select>
            <a-input
              :value="readPipelineArg(record, 'value')"
              size="small"
              style="width: 120px"
              :placeholder="t('common.description', '参数')"
              @change="onPipelineArg(index, 'value', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-button danger type="link" size="small" @click="removeRow(index)">{{ t("common.delete", "删除") }}</a-button>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import type { DynamicFieldType } from "@/types/dynamic-tables";
import type { OutputFieldMapping, TransformOp } from "@/types/dynamic-dataflow";

const props = defineProps<{
  mappings: OutputFieldMapping[];
}>();

const emit = defineEmits<{
  (e: "update:mappings", value: OutputFieldMapping[]): void;
}>();

const { t } = useI18n();

const fieldTypes: DynamicFieldType[] = ["Int", "Long", "Decimal", "String", "Text", "Bool", "DateTime", "Date"];
const pipelineOps: TransformOp["type"][] = ["trim", "upper", "lower", "replace", "concat", "cast", "expr", "lookup"];
const localMappings = ref<OutputFieldMapping[]>([]);

watch(
  () => props.mappings,
  value => {
    localMappings.value = value.map(item => ({ ...item, source: item.source ? { ...item.source } : undefined, pipeline: [...item.pipeline] }));
  },
  { immediate: true, deep: true }
);

const columns = computed(() => [
  { title: t("dynamicDesigner.targetField"), key: "targetFieldKey", width: 140 },
  { title: t("designer.entityModeling.displayName"), key: "targetLabel", width: 140 },
  { title: t("dynamicDesigner.sourceField"), key: "source", width: 140 },
  { title: t("dynamicDesigner.type"), key: "type", width: 120 },
  { title: "Null", key: "nullable", width: 70 },
  { title: "onError", key: "onError", width: 120 },
  { title: t("dynamicDesigner.pipeline"), key: "pipeline", width: 240 },
  { title: t("common.actions"), key: "actions", width: 90 }
]);

function sync() {
  emit("update:mappings", localMappings.value.map(item => ({ ...item, source: item.source ? { ...item.source } : undefined, pipeline: [...item.pipeline] })));
}

function addRow() {
  localMappings.value.push({
    targetFieldKey: `field_${localMappings.value.length + 1}`,
    targetLabel: `Field ${localMappings.value.length + 1}`,
    targetType: "String",
    nullable: true,
    source: undefined,
    pipeline: [],
    onError: "null"
  });
  sync();
}

function removeRow(index: number) {
  localMappings.value.splice(index, 1);
  sync();
}

function onText(index: number, key: "targetFieldKey" | "targetLabel" | "targetType" | "onError", value: string) {
  const next = { ...localMappings.value[index] } as OutputFieldMapping;
  if (key === "targetFieldKey") next.targetFieldKey = value.trim();
  if (key === "targetLabel") next.targetLabel = value;
  if (key === "targetType") next.targetType = value as DynamicFieldType;
  if (key === "onError") next.onError = value as OutputFieldMapping["onError"];
  localMappings.value[index] = next;
  sync();
}

function onNullable(index: number, checked: boolean) {
  localMappings.value[index] = { ...localMappings.value[index], nullable: checked };
  sync();
}

function onSourceField(index: number, fieldKey: string) {
  const current = localMappings.value[index];
  localMappings.value[index] = {
    ...current,
    source: fieldKey.trim()
      ? { nodeId: current.source?.nodeId ?? "source", fieldKey: fieldKey.trim() }
      : undefined
  };
  sync();
}

function onPipelineType(index: number, type: TransformOp["type"]) {
  const mapping = localMappings.value[index];
  localMappings.value[index] = {
    ...mapping,
    pipeline: type ? [{ type, args: mapping.pipeline[0]?.args ?? {} }] : []
  };
  sync();
}

function onPipelineArg(index: number, key: string, value: string) {
  const mapping = localMappings.value[index];
  if (!mapping.pipeline[0]) {
    mapping.pipeline = [{ type: "expr", args: {} }];
  }
  const first = mapping.pipeline[0];
  mapping.pipeline = [{ ...first, args: { ...(first.args ?? {}), [key]: value } }];
  localMappings.value[index] = { ...mapping };
  sync();
}

function readPipelineArg(record: OutputFieldMapping, key: string) {
  const value = record.pipeline[0]?.args?.[key];
  return typeof value === "string" ? value : "";
}
</script>

<style scoped>
.field-mapping-panel {
  padding: 8px;
}

.toolbar {
  margin-bottom: 8px;
}

.pipeline-cell {
  display: flex;
  gap: 6px;
}
</style>
