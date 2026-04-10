<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.knowledgeSearch.datasetId')">
      <a-input v-model:value="datasetId" placeholder="dataset_xxx" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeSearch.queryPath')">
      <a-input v-model:value="queryPath" placeholder="input.query" @change="emitChange" />
    </a-form-item>
    <a-form-item label="TopK">
      <a-input-number v-model:value="topK" :min="1" :max="50" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeSearch.minScore')">
      <a-input-number v-model:value="minScore" :min="0" :max="1" :step="0.01" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeSearch.outputKey')">
      <a-input v-model:value="outputKey" placeholder="knowledge_hits" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const datasetId = computed<string>({
  get: () => (typeof props.configs.datasetId === "string" ? (props.configs.datasetId as string) : ((props.configs.datasetId = ""), "")),
  set: (value) => (props.configs.datasetId = value)
});
const queryPath = computed<string>({
  get: () => (typeof props.configs.queryPath === "string" ? (props.configs.queryPath as string) : ((props.configs.queryPath = "input.query"), "input.query")),
  set: (value) => (props.configs.queryPath = value)
});
const topK = computed<number>({
  get: () => (typeof props.configs.topK === "number" ? (props.configs.topK as number) : ((props.configs.topK = 5), 5)),
  set: (value) => (props.configs.topK = value)
});
const minScore = computed<number>({
  get: () => (typeof props.configs.minScore === "number" ? (props.configs.minScore as number) : ((props.configs.minScore = 0.5), 0.5)),
  set: (value) => (props.configs.minScore = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "knowledge_hits"), "knowledge_hits")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
