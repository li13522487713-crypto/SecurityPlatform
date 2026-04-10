<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="变量名">
      <a-input v-model:value="variableName" placeholder="target_var" @change="emitChange" />
    </a-form-item>
    <a-form-item label="赋值表达式">
      <a-textarea v-model:value="valueExpression" :rows="4" placeholder="{{input.value}}" @change="emitChange" />
    </a-form-item>
    <a-form-item label="作用域">
      <a-select v-model:value="scope" @change="emitChange">
        <a-select-option value="workflow">工作流</a-select-option>
        <a-select-option value="loop">循环作用域</a-select-option>
        <a-select-option value="session">会话作用域</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item label="覆盖已存在值">
      <a-switch v-model:checked="overwrite" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

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
