<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="模型提供商">
      <a-input v-model:value="provider" placeholder="openai / azure / deepseek" @change="emitChange" />
    </a-form-item>
    <a-form-item label="模型">
      <a-input v-model:value="model" placeholder="gpt-5.4-medium" @change="emitChange" />
    </a-form-item>
    <a-form-item label="系统提示词">
      <VariableRefPicker v-model:model-value="systemPrompt" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item label="用户提示词模板">
      <VariableRefPicker v-model:model-value="prompt" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item label="温度">
      <a-slider v-model:value="temperature" :min="0" :max="2" :step="0.1" @change="emitChange" />
    </a-form-item>
    <a-form-item label="最大输出Token">
      <a-input-number v-model:value="maxTokens" :min="1" :max="32768" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="流式输出">
      <a-switch v-model:checked="stream" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="llm_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import VariableRefPicker from "@/components/workflow/widgets/VariableRefPicker.vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

const provider = computed<string>({
  get: () => (typeof props.configs.provider === "string" ? (props.configs.provider as string) : ((props.configs.provider = "openai"), "openai")),
  set: (value) => (props.configs.provider = value)
});
const model = computed<string>({
  get: () => (typeof props.configs.model === "string" ? (props.configs.model as string) : ((props.configs.model = "gpt-5.4-medium"), "gpt-5.4-medium")),
  set: (value) => (props.configs.model = value)
});
const systemPrompt = computed<string>({
  get: () => (typeof props.configs.systemPrompt === "string" ? (props.configs.systemPrompt as string) : ((props.configs.systemPrompt = ""), "")),
  set: (value) => (props.configs.systemPrompt = value)
});
const prompt = computed<string>({
  get: () => (typeof props.configs.prompt === "string" ? (props.configs.prompt as string) : ((props.configs.prompt = "{{input.message}}"), "{{input.message}}")),
  set: (value) => (props.configs.prompt = value)
});
const temperature = computed<number>({
  get: () => (typeof props.configs.temperature === "number" ? (props.configs.temperature as number) : ((props.configs.temperature = 0.7), 0.7)),
  set: (value) => (props.configs.temperature = value)
});
const maxTokens = computed<number>({
  get: () => (typeof props.configs.maxTokens === "number" ? (props.configs.maxTokens as number) : ((props.configs.maxTokens = 2048), 2048)),
  set: (value) => (props.configs.maxTokens = value)
});
const stream = computed<boolean>({
  get: () => (typeof props.configs.stream === "boolean" ? (props.configs.stream as boolean) : ((props.configs.stream = true), true)),
  set: (value) => (props.configs.stream = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "llm_output"), "llm_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
