<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.intentDetector.intents')">
      <a-textarea v-model:value="intentText" :rows="5" @change="syncIntents" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.intentDetector.inputPath')">
      <a-input v-model:value="inputPath" placeholder="input.message" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.intentDetector.threshold')">
      <a-input-number v-model:value="threshold" :min="0" :max="1" :step="0.01" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.intentDetector.outputKey')">
      <a-input v-model:value="outputKey" placeholder="intent_result" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown> }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const intents = computed<string[]>({
  get() {
    if (!Array.isArray(props.configs.intents)) {
      props.configs.intents = [];
    }
    return props.configs.intents as string[];
  },
  set(value) {
    props.configs.intents = value;
  }
});

const intentText = ref("");
watch(
  intents,
  (value) => {
    intentText.value = value.join("\n");
  },
  { immediate: true, deep: true }
);

const inputPath = computed<string>({
  get: () => (typeof props.configs.inputPath === "string" ? (props.configs.inputPath as string) : ((props.configs.inputPath = "input.message"), "input.message")),
  set: (value) => (props.configs.inputPath = value)
});
const threshold = computed<number>({
  get: () => (typeof props.configs.threshold === "number" ? (props.configs.threshold as number) : ((props.configs.threshold = 0.6), 0.6)),
  set: (value) => (props.configs.threshold = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "intent_result"), "intent_result")),
  set: (value) => (props.configs.outputKey = value)
});

function syncIntents() {
  intents.value = intentText.value
    .split("\n")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  emitChange();
}

function emitChange() {
  emit("change");
}
</script>
