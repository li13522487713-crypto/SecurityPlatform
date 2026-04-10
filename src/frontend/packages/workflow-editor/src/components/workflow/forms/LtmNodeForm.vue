<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.ltm.action')">
      <a-select v-model:value="action" @change="emitChange">
        <a-select-option value="read">{{ t('wfUi.forms.ltm.actionRead') }}</a-select-option>
        <a-select-option value="write">{{ t('wfUi.forms.ltm.actionWrite') }}</a-select-option>
        <a-select-option value="delete">{{ t('wfUi.forms.ltm.actionDelete') }}</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.ltm.namespace')">
      <a-input v-model:value="namespace" placeholder="default" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.ltm.keyName')">
      <a-input v-model:value="keyName" placeholder="memory_key" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="action === 'write'" :label="t('wfUi.forms.ltm.valuePath')">
      <a-input v-model:value="valuePath" placeholder="input.value" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.ltm.outputKey')">
      <a-input v-model:value="outputKey" placeholder="ltm_result" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const action = computed<string>({
  get: () => (typeof props.configs.action === "string" ? (props.configs.action as string) : ((props.configs.action = "read"), "read")),
  set: (value) => (props.configs.action = value)
});
const namespace = computed<string>({
  get: () => (typeof props.configs.namespace === "string" ? (props.configs.namespace as string) : ((props.configs.namespace = "default"), "default")),
  set: (value) => (props.configs.namespace = value)
});
const keyName = computed<string>({
  get: () => (typeof props.configs.keyName === "string" ? (props.configs.keyName as string) : ((props.configs.keyName = ""), "")),
  set: (value) => (props.configs.keyName = value)
});
const valuePath = computed<string>({
  get: () => (typeof props.configs.valuePath === "string" ? (props.configs.valuePath as string) : ((props.configs.valuePath = "input.value"), "input.value")),
  set: (value) => (props.configs.valuePath = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "ltm_result"), "ltm_result")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
