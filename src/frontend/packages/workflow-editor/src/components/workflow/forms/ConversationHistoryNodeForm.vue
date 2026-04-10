<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.conversationHistory.nodeType')">
      <a-tag color="magenta">{{ nodeType }}</a-tag>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.conversationHistory.conversationId')">
      <a-input v-model:value="conversationId" placeholder="conversation_id" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'ConversationHistory'" :label="t('wfUi.forms.conversationHistory.limit')">
      <a-input-number v-model:value="limit" :min="1" :max="200" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.conversationHistory.outputKey')">
      <a-input v-model:value="outputKey" placeholder="conversation_history" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ configs: Record<string, unknown>; nodeType: string }>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

const nodeType = computed(() => props.nodeType);
const conversationId = computed<string>({
  get: () => (typeof props.configs.conversationId === "string" ? (props.configs.conversationId as string) : ((props.configs.conversationId = ""), "")),
  set: (value) => (props.configs.conversationId = value)
});
const limit = computed<number>({
  get: () => (typeof props.configs.limit === "number" ? (props.configs.limit as number) : ((props.configs.limit = 20), 20)),
  set: (value) => (props.configs.limit = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "conversation_history"), "conversation_history")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
