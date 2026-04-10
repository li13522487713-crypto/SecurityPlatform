<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.json.direction')">
      <a-select v-model:value="direction" @change="emitChange">
        <a-select-option value="serialize">{{ t('wfUi.forms.json.serialize') }}</a-select-option>
        <a-select-option value="deserialize">{{ t('wfUi.forms.json.deserialize') }}</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.json.inputPath')">
      <a-input v-model:value="inputPath" placeholder="input.payload" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.json.pretty')">
      <a-switch v-model:checked="pretty" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.json.outputKey')">
      <a-input v-model:value="outputKey" placeholder="json_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  configs: Record<string, unknown>;
  directionDefault: "serialize" | "deserialize";
}>();

const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const direction = computed<string>({
  get: () =>
    typeof props.configs.direction === "string"
      ? (props.configs.direction as string)
      : ((props.configs.direction = props.directionDefault), props.directionDefault),
  set: (value) => (props.configs.direction = value)
});
const inputPath = computed<string>({
  get: () => (typeof props.configs.inputPath === "string" ? (props.configs.inputPath as string) : ((props.configs.inputPath = "input.payload"), "input.payload")),
  set: (value) => (props.configs.inputPath = value)
});
const pretty = computed<boolean>({
  get: () => (typeof props.configs.pretty === "boolean" ? (props.configs.pretty as boolean) : ((props.configs.pretty = true), true)),
  set: (value) => (props.configs.pretty = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "json_output"), "json_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
