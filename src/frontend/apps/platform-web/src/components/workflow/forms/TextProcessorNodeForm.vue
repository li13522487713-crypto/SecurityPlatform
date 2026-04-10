<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="处理模式">
      <a-select v-model:value="mode" @change="emitChange">
        <a-select-option value="template">模板渲染</a-select-option>
        <a-select-option value="replace">替换</a-select-option>
        <a-select-option value="extract">提取</a-select-option>
      </a-select>
    </a-form-item>
    <a-form-item label="输入变量路径">
      <a-input v-model:value="inputPath" placeholder="input.text" @change="emitChange" />
    </a-form-item>
    <a-form-item label="处理模板 / 表达式">
      <a-textarea v-model:value="templateText" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="text_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

const mode = computed<string>({
  get: () => (typeof props.configs.mode === "string" ? (props.configs.mode as string) : ((props.configs.mode = "template"), "template")),
  set: (value) => (props.configs.mode = value)
});
const inputPath = computed<string>({
  get: () => (typeof props.configs.inputPath === "string" ? (props.configs.inputPath as string) : ((props.configs.inputPath = "input.text"), "input.text")),
  set: (value) => (props.configs.inputPath = value)
});
const templateText = computed<string>({
  get: () => (typeof props.configs.templateText === "string" ? (props.configs.templateText as string) : ((props.configs.templateText = "{{input.text}}"), "{{input.text}}")),
  set: (value) => (props.configs.templateText = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "text_output"), "text_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
