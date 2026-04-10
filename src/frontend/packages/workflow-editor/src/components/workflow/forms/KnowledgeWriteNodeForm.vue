<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.knowledgeWrite.datasetId')">
      <a-input v-model:value="datasetId" placeholder="dataset_xxx" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeWrite.title')">
      <a-input v-model:value="title" :placeholder="t('wfUi.forms.knowledgeWrite.titlePlaceholder')" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeWrite.contentPath')">
      <a-input v-model:value="contentPath" placeholder="input.content" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeWrite.chunkSize')">
      <a-input-number v-model:value="chunkSize" :min="100" :max="4000" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.knowledgeWrite.outputKey')">
      <a-input v-model:value="outputKey" placeholder="knowledge_write_result" @change="emitChange" />
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
const title = computed<string>({
  get: () => (typeof props.configs.title === "string" ? (props.configs.title as string) : ((props.configs.title = ""), "")),
  set: (value) => (props.configs.title = value)
});
const contentPath = computed<string>({
  get: () => (typeof props.configs.contentPath === "string" ? (props.configs.contentPath as string) : ((props.configs.contentPath = "input.content"), "input.content")),
  set: (value) => (props.configs.contentPath = value)
});
const chunkSize = computed<number>({
  get: () => (typeof props.configs.chunkSize === "number" ? (props.configs.chunkSize as number) : ((props.configs.chunkSize = 800), 800)),
  set: (value) => (props.configs.chunkSize = value)
});
const outputKey = computed<string>({
  get: () =>
    typeof props.configs.outputKey === "string"
      ? (props.configs.outputKey as string)
      : ((props.configs.outputKey = "knowledge_write_result"), "knowledge_write_result"),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
