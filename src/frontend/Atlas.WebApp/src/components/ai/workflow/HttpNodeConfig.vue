<template>
  <a-form layout="vertical">
    <a-form-item label="URL">
      <a-input v-model:value="local.url" placeholder="https://api.example.com/endpoint" />
    </a-form-item>
    <a-form-item label="Method">
      <a-select v-model:value="local.method" :options="methodOptions" />
    </a-form-item>
    <a-form-item label="Body Template">
      <a-textarea v-model:value="local.bodyTemplate" :rows="4" placeholder='{"input":"{{input}}"}' />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { reactive, watch } from "vue";

const props = defineProps<{
  modelValue: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: Record<string, unknown>): void;
}>();

const methodOptions = [
  { label: "GET", value: "GET" },
  { label: "POST", value: "POST" },
  { label: "PUT", value: "PUT" },
  { label: "DELETE", value: "DELETE" }
];

const local = reactive({
  url: "",
  method: "GET",
  bodyTemplate: ""
});

watch(
  () => props.modelValue,
  (v) => {
    Object.assign(local, {
      url: (v?.["url"] as string) ?? "",
      method: (v?.["method"] as string) ?? "GET",
      bodyTemplate: (v?.["bodyTemplate"] as string) ?? ""
    });
  },
  { immediate: true, deep: true }
);

watch(
  local,
  () => {
    emit("update:modelValue", {
      ...props.modelValue,
      url: local.url,
      method: local.method,
      bodyTemplate: local.bodyTemplate
    });
  },
  { deep: true }
);
</script>
