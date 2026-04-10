<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.textProcessor.mode')">
      <a-select v-model:value="mode" @change="emitChange">
        <a-select-option value="template">{{ t('wfUi.forms.textProcessor.modeTemplate') }}</a-select-option>
        <a-select-option value="replace">{{ t('wfUi.forms.textProcessor.modeReplace') }}</a-select-option>
        <a-select-option value="extract">{{ t('wfUi.forms.textProcessor.modeExtract') }}</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.textProcessor.inputPath')">
      <a-input v-model:value="inputPath" placeholder="input.text" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.textProcessor.templateText')">
      <a-textarea v-model:value="templateText" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.textProcessor.outputKey')">
      <a-input v-model:value="outputKey" placeholder="text_output" @change="emitChange" />
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
  get: () => (typeof props.configs.mode === "string" ? (props.configs.mode as string) : ((props.configs.mode = "template"), "template")),
  set: (value) => (props.configs.mode = value)
});
const inputPath = computed<string>({
  get: () => (typeof props.configs.inputPath === "string" ? (props.configs.inputPath as string) : ((props.configs.inputPath = "input.text"), "input.text")),
  set: (value) => (props.configs.inputPath = value)
});
const templateText = computed<string>({
  get: () => (typeof props.configs.templateText === "string" ? (props.configs.templateText as string) : ((props.configs.templateText = "{{input.text}}"), "{{input.text}}")),
  set: (value) => (props.configs.templateText = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "text_output"), "text_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
