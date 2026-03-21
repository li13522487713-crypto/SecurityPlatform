<template>
  <div :class="['chat-message', `chat-message--${message.role}`]">
    <div class="chat-message__avatar">
      <a-avatar v-if="message.role === 'user'" :size="32" style="background-color: #1677ff">
        {{ userInitial }}
      </a-avatar>
      <a-avatar v-else :size="32" style="background-color: #52c41a">
        {{ t("ai.chat.avatarAssistant") }}
      </a-avatar>
    </div>
    <div class="chat-message__bubble">
      <template v-if="message.role === 'assistant'">
        <MarkdownRenderer :content="message.content" />
        <span v-if="message.isStreaming" class="typing-cursor">▋</span>
      </template>
      <template v-else>
        <div class="user-text">{{ message.content }}</div>
      </template>
      <div class="chat-message__time">
        {{ formatTime(message.createdAt) }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import MarkdownRenderer from "./MarkdownRenderer.vue";
import type { StreamChatMessage } from "@/composables/useStreamChat";
import { getActiveLocale } from "@/i18n";

const { t } = useI18n();

const props = defineProps<{
  message: StreamChatMessage;
  userInitial?: string;
}>();

const userInitial = computed(() => props.userInitial || "U");

function formatTime(iso: string): string {
  try {
    const date = new Date(iso);
    const loc = getActiveLocale() === "en-US" ? "en-US" : "zh-CN";
    return date.toLocaleTimeString(loc, {
      hour: "2-digit",
      minute: "2-digit"
    });
  } catch {
    return "";
  }
}
</script>

<style scoped>
.chat-message {
  display: flex;
  gap: 10px;
  margin-bottom: 16px;
  align-items: flex-start;
}

.chat-message--user {
  flex-direction: row-reverse;
}

.chat-message__avatar {
  flex-shrink: 0;
}

.chat-message__bubble {
  max-width: 70%;
  padding: 10px 14px;
  border-radius: 8px;
  position: relative;
}

.chat-message--user .chat-message__bubble {
  background: #1677ff;
  color: #fff;
  border-top-right-radius: 2px;
}

.chat-message--assistant .chat-message__bubble {
  background: #f5f5f5;
  color: rgba(0, 0, 0, 0.85);
  border-top-left-radius: 2px;
}

.user-text {
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
}

.chat-message__time {
  font-size: 11px;
  margin-top: 4px;
  opacity: 0.6;
}

.chat-message--user .chat-message__time {
  text-align: right;
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

.typing-cursor {
  display: inline-block;
  animation: blink 1s infinite;
  color: #52c41a;
}
</style>
