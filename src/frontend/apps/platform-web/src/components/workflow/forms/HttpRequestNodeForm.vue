<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="请求方法">
      <a-select v-model:value="method" @change="emitChange">
        <a-select-option value="GET">GET</a-select-option>
        <a-select-option value="POST">POST</a-select-option>
        <a-select-option value="PUT">PUT</a-select-option>
        <a-select-option value="PATCH">PATCH</a-select-option>
        <a-select-option value="DELETE">DELETE</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item label="请求 URL">
      <a-input v-model:value="url" @change="emitChange" />
    </a-form-item>
    <a-form-item label="请求头 JSON">
      <a-textarea v-model:value="headersJson" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item label="请求体">
      <a-textarea v-model:value="body" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item label="超时（毫秒）">
      <a-input-number v-model:value="timeoutMs" :min="0" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="http_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

const method = computed<string>({
  get: () => (typeof props.configs.method === "string" ? (props.configs.method as string) : ((props.configs.method = "GET"), "GET")),
  set: (value) => (props.configs.method = value)
});
const url = computed<string>({
  get: () => (typeof props.configs.url === "string" ? (props.configs.url as string) : ((props.configs.url = ""), "")),
  set: (value) => (props.configs.url = value)
});
const headersJson = computed<string>({
  get: () => (typeof props.configs.headersJson === "string" ? (props.configs.headersJson as string) : ((props.configs.headersJson = "{}"), "{}")),
  set: (value) => (props.configs.headersJson = value)
});
const body = computed<string>({
  get: () => (typeof props.configs.body === "string" ? (props.configs.body as string) : ((props.configs.body = ""), "")),
  set: (value) => (props.configs.body = value)
});
const timeoutMs = computed<number>({
  get: () => (typeof props.configs.timeoutMs === "number" ? (props.configs.timeoutMs as number) : ((props.configs.timeoutMs = 15000), 15000)),
  set: (value) => (props.configs.timeoutMs = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "http_output"), "http_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
