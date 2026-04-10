<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.assignVariable.variableName')">
      <a-input v-model:value="variableName" placeholder="target_var" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.assignVariable.valueExpression')">
      <a-textarea v-model:value="valueExpression" :rows="4" placeholder="{{input.value}}" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.assignVariable.scope')">
      <a-select v-model:value="scope" @change="emitChange">
        <a-select-option value="workflow">{{ t('wfUi.forms.assignVariable.scopeWorkflow') }}</a-select-option>
        <a-select-option value="loop">{{ t('wfUi.forms.assignVariable.scopeLoop') }}</a-select-option>
        <a-select-option value="session">{{ t('wfUi.forms.assignVariable.scopeSession') }}</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.assignVariable.overwrite')">
      <a-switch v-model:checked="overwrite" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const variableName = computed<string>({
  get: () => (typeof props.configs.variableName === "string" ? (props.configs.variableName as string) : ((props.configs.variableName = ""), "")),
  set: (value) => (props.configs.variableName = value)
});
const valueExpression = computed<string>({
  get: () => (typeof props.configs.valueExpression === "string" ? (props.configs.valueExpression as string) : ((props.configs.valueExpression = ""), "")),
  set: (value) => (props.configs.valueExpression = value)
});
const scope = computed<string>({
  get: () => (typeof props.configs.scope === "string" ? (props.configs.scope as string) : ((props.configs.scope = "workflow"), "workflow")),
  set: (value) => (props.configs.scope = value)
});
const overwrite = computed<boolean>({
  get: () => (typeof props.configs.overwrite === "boolean" ? (props.configs.overwrite as boolean) : ((props.configs.overwrite = true), true)),
  set: (value) => (props.configs.overwrite = value)
});

function emitChange() {
  emit("change");
}
</script>
