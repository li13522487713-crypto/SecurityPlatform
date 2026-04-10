<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.conversation.nodeType')">
      <a-tag color="purple">{{ nodeType }}</a-tag>
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.conversation.conversationId')">
      <a-input v-model:value="conversationId" placeholder="conversation_id" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.conversation.userId')">
      <a-input v-model:value="userId" placeholder="user_id" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'CreateConversation' || nodeType === 'ConversationUpdate'" :label="t('wfUi.forms.conversation.title')">
      <a-input v-model:value="title" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'ConversationList'" :label="t('wfUi.forms.conversation.agentId')">
      <a-input v-model:value="agentId" placeholder="agent_id" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.conversation.outputKey')">
      <a-input v-model:value="outputKey" placeholder="conversation_output" @change="emitChange" />
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
const userId = computed<string>({
  get: () => (typeof props.configs.userId === "string" ? (props.configs.userId as string) : ((props.configs.userId = ""), "")),
  set: (value) => (props.configs.userId = value)
});
const title = computed<string>({
  get: () => (typeof props.configs.title === "string" ? (props.configs.title as string) : ((props.configs.title = ""), "")),
  set: (value) => (props.configs.title = value)
});
const agentId = computed<string>({
  get: () => (typeof props.configs.agentId === "string" ? (props.configs.agentId as string) : ((props.configs.agentId = ""), "")),
  set: (value) => (props.configs.agentId = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "conversation_output"), "conversation_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
