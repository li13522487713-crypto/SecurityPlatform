<template>
  <div class="condition-expression-editor">
    <div v-for="(cond, idx) in conditions" :key="idx" class="condition-row">
      <a-select
        v-if="idx > 0"
        v-model:value="cond.logic"
        size="small"
        style="width: 70px"
      >
        <a-select-option value="&&">{{ t("designer.conditionEditor.and") }}</a-select-option>
        <a-select-option value="||">{{ t("designer.conditionEditor.or") }}</a-select-option>
      </a-select>
      <a-select
        v-model:value="cond.field"
        size="small"
        :placeholder="t('designer.conditionEditor.field')"
        show-search
        style="flex: 1"
        :options="fieldOptions"
      />
      <a-select
        v-model:value="cond.operator"
        size="small"
        style="width: 100px"
      >
        <a-select-option value="===">{{ t("designer.conditionEditor.eq") }}</a-select-option>
        <a-select-option value="!==">{{ t("designer.conditionEditor.neq") }}</a-select-option>
        <a-select-option value=">">{{ t("designer.conditionEditor.gt") }}</a-select-option>
        <a-select-option value="<">{{ t("designer.conditionEditor.lt") }}</a-select-option>
      </a-select>
      <a-input
        v-model:value="cond.value"
        size="small"
        :placeholder="t('designer.conditionEditor.value')"
        style="flex: 1"
      />
      <a-button type="link" danger size="small" @click="removeCondition(idx)">x</a-button>
    </div>
    <a-button size="small" @click="addCondition">
      {{ t("designer.conditionEditor.addCondition") }}
    </a-button>
    <div v-if="expression" class="expression-preview">
      <code>{{ expression }}</code>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

interface ConditionRow {
  logic: string;
  field: string;
  operator: string;
  value: string;
}

interface Props {
  modelValue?: string;
  fields?: { label: string; value: string }[];
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: "",
  fields: () => [],
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const fieldOptions = computed(() =>
  props.fields.map((f) => ({ label: f.label, value: f.value })),
);

const conditions = reactive<ConditionRow[]>([
  { logic: "&&", field: "", operator: "===", value: "" },
]);

const expression = computed(() => {
  const parts = conditions
    .filter((c) => c.field && c.value)
    .map((c, idx) => {
      const valueExpr = isNaN(Number(c.value)) ? `'${c.value}'` : c.value;
      const cond = `data.${c.field} ${c.operator} ${valueExpr}`;
      return idx === 0 ? cond : `${c.logic} ${cond}`;
    });
  return parts.join(" ");
});

function addCondition() {
  conditions.push({ logic: "&&", field: "", operator: "===", value: "" });
}

function removeCondition(idx: number) {
  if (conditions.length > 1) {
    conditions.splice(idx, 1);
  }
}

watch(expression, (val) => {
  emit("update:modelValue", val);
});
</script>

<style scoped>
.condition-expression-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.condition-row {
  display: flex;
  gap: 4px;
  align-items: center;
}

.expression-preview {
  font-size: 12px;
  padding: 4px 8px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
}

.expression-preview code {
  font-family: monospace;
  color: #1890ff;
  font-size: 12px;
}
</style>
