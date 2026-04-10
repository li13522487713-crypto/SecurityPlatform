<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="操作类型">
      <a-select v-model:value="action" @change="emitChange">
        <a-select-option value="read">读取</a-select-option>
        <a-select-option value="write">写入</a-select-option>
        <a-select-option value="delete">删除</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item label="命名空间">
      <a-input v-model:value="namespace" placeholder="default" @change="emitChange" />
    </a-form-item>
    <a-form-item label="键">
      <a-input v-model:value="keyName" placeholder="memory_key" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="action === 'write'" label="写入内容变量路径">
      <a-input v-model:value="valuePath" placeholder="input.value" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="ltm_result" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

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
