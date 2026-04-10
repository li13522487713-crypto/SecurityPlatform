<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="转换方向">
      <a-select v-model:value="direction" @change="emitChange">
        <a-select-option value="serialize">对象 → JSON</a-select-option>
        <a-select-option value="deserialize">JSON → 对象</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item label="输入变量路径">
      <a-input v-model:value="inputPath" placeholder="input.payload" @change="emitChange" />
    </a-form-item>
    <a-form-item label="格式化输出">
      <a-switch v-model:checked="pretty" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="json_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  configs: Record<string, unknown>;
  directionDefault: "serialize" | "deserialize";
}>();

const emit = defineEmits<{ (e: "change"): void }>();

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
