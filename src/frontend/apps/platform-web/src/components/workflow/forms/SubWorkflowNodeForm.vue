<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="子工作流 ID">
      <a-input v-model:value="subWorkflowId" placeholder="workflow_xxx" @change="emitChange" />
    </a-form-item>
    <a-form-item label="继承父变量">
      <a-switch v-model:checked="inheritVariables" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="!inheritVariables" label="输入变量路径">
      <a-input v-model:value="inputsVariable" placeholder="input" @change="emitChange" />
    </a-form-item>
    <a-form-item label="最大嵌套深度">
      <a-input-number v-model:value="maxDepth" :min="1" :max="10" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="subworkflow_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

const subWorkflowId = computed<string>({
  get: () => (typeof props.configs.subWorkflowId === "string" ? (props.configs.subWorkflowId as string) : ((props.configs.subWorkflowId = ""), "")),
  set: (value) => (props.configs.subWorkflowId = value)
});
const inheritVariables = computed<boolean>({
  get: () => (typeof props.configs.inheritVariables === "boolean" ? (props.configs.inheritVariables as boolean) : ((props.configs.inheritVariables = true), true)),
  set: (value) => (props.configs.inheritVariables = value)
});
const inputsVariable = computed<string>({
  get: () => (typeof props.configs.inputsVariable === "string" ? (props.configs.inputsVariable as string) : ((props.configs.inputsVariable = "input"), "input")),
  set: (value) => (props.configs.inputsVariable = value)
});
const maxDepth = computed<number>({
  get: () => (typeof props.configs.maxDepth === "number" ? (props.configs.maxDepth as number) : ((props.configs.maxDepth = 4), 4)),
  set: (value) => (props.configs.maxDepth = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "subworkflow_output"), "subworkflow_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
