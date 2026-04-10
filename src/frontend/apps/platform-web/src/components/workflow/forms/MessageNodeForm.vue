<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="节点类型">
      <a-tag color="cyan">{{ nodeType }}</a-tag>
    </a-form-item>
    <a-form-item label="会话 ID">
      <a-input v-model:value="conversationId" placeholder="conversation_id" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'EditMessage' || nodeType === 'DeleteMessage'" label="消息 ID">
      <a-input v-model:value="messageId" placeholder="message_id" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'CreateMessage' || nodeType === 'EditMessage'" label="消息内容">
      <a-textarea v-model:value="content" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'MessageList'" label="分页大小">
      <a-input-number v-model:value="pageSize" :min="1" :max="100" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="message_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ configs: Record<string, unknown>; nodeType: string }>();
const emit = defineEmits<{ (e: "change"): void }>();

const nodeType = computed(() => props.nodeType);
const conversationId = computed<string>({
  get: () => (typeof props.configs.conversationId === "string" ? (props.configs.conversationId as string) : ((props.configs.conversationId = ""), "")),
  set: (value) => (props.configs.conversationId = value)
});
const messageId = computed<string>({
  get: () => (typeof props.configs.messageId === "string" ? (props.configs.messageId as string) : ((props.configs.messageId = ""), "")),
  set: (value) => (props.configs.messageId = value)
});
const content = computed<string>({
  get: () => (typeof props.configs.content === "string" ? (props.configs.content as string) : ((props.configs.content = ""), "")),
  set: (value) => (props.configs.content = value)
});
const pageSize = computed<number>({
  get: () => (typeof props.configs.pageSize === "number" ? (props.configs.pageSize as number) : ((props.configs.pageSize = 20), 20)),
  set: (value) => (props.configs.pageSize = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "message_output"), "message_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
