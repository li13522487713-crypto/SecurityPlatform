<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="节点类型">
      <a-tag color="magenta">{{ nodeType }}</a-tag>
    </a-form-item>
    <a-form-item label="会话 ID">
      <a-input v-model:value="conversationId" placeholder="conversation_id" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="nodeType === 'ConversationHistory'" label="查询条数">
      <a-input-number v-model:value="limit" :min="1" :max="200" style="width: 100%" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="conversation_history" @change="emitChange" />
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
