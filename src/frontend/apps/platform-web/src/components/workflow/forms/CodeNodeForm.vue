<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="执行语言">
      <a-select v-model:value="language" @change="emitChange">
        <a-select-option value="javascript">JavaScript</a-select-option>
        <a-select-option value="typescript">TypeScript</a-select-option>
        <a-select-option value="python">Python</a-select-option>
      </a-select>
    </a-form-item>

    <a-form-item label="代码（Monaco）">
      <MonacoCodeEditor v-model:value="code" :language="language" />
    </a-form-item>

    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="code_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import MonacoCodeEditor from "@/components/workflow/forms/MonacoCodeEditor.vue";

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const language = computed<string>({
  get: () => (typeof props.configs.language === "string" ? (props.configs.language as string) : ((props.configs.language = "javascript"), "javascript")),
  set: (value) => (props.configs.language = value)
});

const code = computed<string>({
  get: () =>
    typeof props.configs.code === "string"
      ? (props.configs.code as string)
      : ((props.configs.code = "return input;"), "return input;"),
  set: (value) => (props.configs.code = value)
});

const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "code_output"), "code_output")),
  set: (value) => (props.configs.outputKey = value)
});

watch([language, code, outputKey], () => emitChange(), { deep: true });

function emitChange() {
  emit("change");
}
</script>
