<template>
  <a-form layout="vertical">
    <a-form-item label="模型">
      <a-input v-model:value="local.model" placeholder="gpt-4o-mini" />
    </a-form-item>
    <a-form-item label="Prompt 模板">
      <a-textarea v-model:value="local.promptTemplate" :rows="4" placeholder="例如：请总结 {{input}}" />
    </a-form-item>
    <a-form-item label="温度">
      <a-input-number v-model:value="local.temperature" :min="0" :max="2" :step="0.1" />
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

const local = reactive({
  model: "",
  promptTemplate: "",
  temperature: 0.7
});

watch(
  () => props.modelValue,
  (v) => {
    Object.assign(local, {
      model: (v?.["model"] as string) ?? "",
      promptTemplate: (v?.["promptTemplate"] as string) ?? "",
      temperature: Number(v?.["temperature"] ?? 0.7)
    });
  },
  { immediate: true, deep: true }
);

watch(
  local,
  () => {
    emit("update:modelValue", {
      ...props.modelValue,
      model: local.model,
      promptTemplate: local.promptTemplate,
      temperature: local.temperature
    });
  },
  { deep: true }
);
</script>
