<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="问题模板">
      <a-textarea v-model:value="questionTemplate" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item label="等待超时（秒）">
      <a-input-number v-model:value="timeoutSeconds" :min="1" :max="3600" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="答案变量名">
      <a-input v-model:value="answerKey" placeholder="qa_answer" @change="emitChange" />
    </a-form-item>
    <a-form-item label="允许空答案">
      <a-switch v-model:checked="allowEmpty" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();

const questionTemplate = computed<string>({
  get: () =>
    typeof props.configs.questionTemplate === "string"
      ? (props.configs.questionTemplate as string)
      : ((props.configs.questionTemplate = "请补充必要信息。"), "请补充必要信息。"),
  set: (value) => (props.configs.questionTemplate = value)
});
const timeoutSeconds = computed<number>({
  get: () => (typeof props.configs.timeoutSeconds === "number" ? (props.configs.timeoutSeconds as number) : ((props.configs.timeoutSeconds = 300), 300)),
  set: (value) => (props.configs.timeoutSeconds = value)
});
const answerKey = computed<string>({
  get: () => (typeof props.configs.answerKey === "string" ? (props.configs.answerKey as string) : ((props.configs.answerKey = "qa_answer"), "qa_answer")),
  set: (value) => (props.configs.answerKey = value)
});
const allowEmpty = computed<boolean>({
  get: () => (typeof props.configs.allowEmpty === "boolean" ? (props.configs.allowEmpty as boolean) : ((props.configs.allowEmpty = false), false)),
  set: (value) => (props.configs.allowEmpty = value)
});

function emitChange() {
  emit("change");
}
</script>
