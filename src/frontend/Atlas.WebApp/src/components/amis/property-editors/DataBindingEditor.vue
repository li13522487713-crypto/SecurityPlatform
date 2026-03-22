<template>
  <div class="data-binding-editor">
    <a-select
      v-model:value="selectedField"
      :placeholder="t('designer.dataBinding.placeholder')"
      show-search
      allow-clear
      :options="fieldOptions"
      :filter-option="filterOption"
      style="width: 100%"
      @change="handleFieldSelect"
    />
    <div v-if="expression" class="binding-preview">
      <code>{{ expression }}</code>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

interface FieldOption {
  label: string;
  value: string;
  fieldType?: string;
}

interface Props {
  modelValue?: string;
  fields?: FieldOption[];
  tableKey?: string;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: "",
  fields: () => [],
  tableKey: "",
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const selectedField = ref<string | undefined>(
  props.modelValue ? props.modelValue.replace(/^\$\{|\}$/g, "") : undefined,
);

const fieldOptions = computed(() =>
  props.fields.map((f) => ({
    label: `${f.label} (${f.value})`,
    value: f.value,
  })),
);

const expression = computed(() =>
  selectedField.value ? `\${${selectedField.value}}` : "",
);

function filterOption(input: string, option: { label: string }) {
  return option.label.toLowerCase().includes(input.toLowerCase());
}

function handleFieldSelect(value: string | undefined) {
  const expr = value ? `\${${value}}` : "";
  emit("update:modelValue", expr);
}

watch(
  () => props.modelValue,
  (val) => {
    selectedField.value = val ? val.replace(/^\$\{|\}$/g, "") : undefined;
  },
);
</script>

<style scoped>
.data-binding-editor {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.binding-preview {
  font-size: 12px;
  color: #999;
  padding: 2px 4px;
  background: #fafafa;
  border-radius: 2px;
}

.binding-preview code {
  font-family: monospace;
  color: #1890ff;
}
</style>
