<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.plugin.pluginKey')">
      <a-input v-model:value="pluginKey" placeholder="plugin.key" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.plugin.method')">
      <a-input v-model:value="method" placeholder="execute" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.plugin.inputJson')">
      <a-textarea v-model:value="inputJson" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.plugin.timeoutMs')">
      <a-input-number v-model:value="timeoutMs" :min="0" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.plugin.outputKey')">
      <a-input v-model:value="outputKey" placeholder="plugin_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const pluginKey = computed<string>({
  get: () => (typeof props.configs.pluginKey === "string" ? (props.configs.pluginKey as string) : ((props.configs.pluginKey = ""), "")),
  set: (value) => (props.configs.pluginKey = value)
});
const method = computed<string>({
  get: () => (typeof props.configs.method === "string" ? (props.configs.method as string) : ((props.configs.method = "execute"), "execute")),
  set: (value) => (props.configs.method = value)
});
const inputJson = computed<string>({
  get: () => (typeof props.configs.inputJson === "string" ? (props.configs.inputJson as string) : ((props.configs.inputJson = "{}"), "{}")),
  set: (value) => (props.configs.inputJson = value)
});
const timeoutMs = computed<number>({
  get: () => (typeof props.configs.timeoutMs === "number" ? (props.configs.timeoutMs as number) : ((props.configs.timeoutMs = 30000), 30000)),
  set: (value) => (props.configs.timeoutMs = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "plugin_output"), "plugin_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
