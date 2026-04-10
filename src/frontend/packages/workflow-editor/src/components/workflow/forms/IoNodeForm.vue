<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.io.nodeType')">
      <a-tag :color="nodeType === 'OutputEmitter' ? 'red' : 'blue'">{{ nodeType }}</a-tag>
    </a-form-item>
    <a-form-item v-if="nodeType === 'OutputEmitter'" :label="t('wfUi.forms.io.templateText')">
      <a-textarea v-model:value="templateText" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'InputReceiver'" :label="t('wfUi.forms.io.prompt')">
      <a-textarea v-model:value="prompt" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'InputReceiver'" :label="t('wfUi.forms.io.timeoutSeconds')">
      <a-input-number v-model:value="timeoutSeconds" :min="1" :max="3600" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.io.outputKey')">
      <a-input v-model:value="outputKey" placeholder="io_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown>; nodeType: string }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const nodeType = computed(() => props.nodeType);
const templateText = computed<string>({
  get: () => (typeof props.configs.templateText === "string" ? (props.configs.templateText as string) : ((props.configs.templateText = ""), "")),
  set: (value) => (props.configs.templateText = value)
});
const prompt = computed<string>({
  get: () =>
    typeof props.configs.prompt === "string"
      ? (props.configs.prompt as string)
      : ((props.configs.prompt = t("wfUi.forms.io.defaultPrompt")), t("wfUi.forms.io.defaultPrompt")),
  set: (value) => (props.configs.prompt = value)
});
const timeoutSeconds = computed<number>({
  get: () => (typeof props.configs.timeoutSeconds === "number" ? (props.configs.timeoutSeconds as number) : ((props.configs.timeoutSeconds = 300), 300)),
  set: (value) => (props.configs.timeoutSeconds = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "io_output"), "io_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
