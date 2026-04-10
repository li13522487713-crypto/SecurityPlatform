<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.variableAggregator.mode')">
      <a-select v-model:value="mode" @change="emitChange">
        <a-select-option value="object">{{ t('wfUi.forms.variableAggregator.modeObject') }}</a-select-option>
        <a-select-option value="array">{{ t('wfUi.forms.variableAggregator.modeArray') }}</a-select-option>
        <a-select-option value="merge">{{ t('wfUi.forms.variableAggregator.modeMerge') }}</a-select-option>
      </a-select>
    </a-form-item>

    <a-form-item :label="t('wfUi.forms.variableAggregator.sourcePaths')">
      <div v-for="(path, index) in sourcePaths" :key="index" class="path-row">
        <a-input v-model:value="sourcePaths[index]" placeholder="input.x" @change="emitChange" />
        <a-button size="small" @click="removePath(index)">-</a-button>
      </div>
      <a-button size="small" type="dashed" @click="addPath">{{ t('wfUi.forms.variableAggregator.addSourcePath') }}</a-button>
    </a-form-item>

    <a-form-item :label="t('wfUi.forms.variableAggregator.outputKey')">
      <a-input v-model:value="outputKey" placeholder="aggregated_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const mode = computed<string>({
  get: () => (typeof props.configs.mode === "string" ? (props.configs.mode as string) : ((props.configs.mode = "object"), "object")),
  set: (value) => (props.configs.mode = value)
});
const sourcePaths = computed<string[]>({
  get() {
    if (!Array.isArray(props.configs.sourcePaths)) {
      props.configs.sourcePaths = [];
    }
    return props.configs.sourcePaths as string[];
  },
  set(value) {
    props.configs.sourcePaths = value;
  }
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "aggregated_output"), "aggregated_output")),
  set: (value) => (props.configs.outputKey = value)
});

function addPath() {
  sourcePaths.value.push("");
  emitChange();
}
function removePath(index: number) {
  sourcePaths.value.splice(index, 1);
  emitChange();
}
function emitChange() {
  emit("change");
}
</script>

<style scoped>
.path-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 32px;
  gap: 8px;
  margin-bottom: 8px;
}
</style>
