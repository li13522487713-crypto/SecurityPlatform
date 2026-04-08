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
    <div class="chat-message__content-area">
      <div class="chat-message__header">
        <span class="chat-message__name">
          {{ message.role === 'user' ? userName : t("ai.chat.avatarAssistant") }}
        </span>
        <span class="chat-message__time">{{ formatTime(message.createdAt) }}</span>
      </div>
      <div class="chat-message__bubble">
        <template v-if="message.role === 'assistant'">
          <DualStreamMessage
            class="chat-message__stream"
            :content="message.content"
            :reasoning-text="message.reasoningText"
            :react-steps="effectiveReactSteps"
            :is-streaming="message.isStreaming"
            :show-typing-cursor="showTypingCursor"
            :reasoning-title="t('ai.chat.thinkPanelTitle')"
            :step-labels="stepLabels"
          />
        </template>
        <template v-else>
          <div class="user-text">{{ message.content }}</div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import {
  DualStreamMessage,
  type ReActStep,
  type StreamChatMessage
} from "@atlas/ai-core";
import { useUserStore } from "@/stores/user";

const { t, locale } = useI18n();
const userStore = useUserStore();

const props = defineProps<{
  message: StreamChatMessage;
  reactSteps?: ReActStep[];
  showTypingCursor?: boolean;
}>();

const userInitial = computed(() => {
  const name = userStore.name || "U";
  return name.charAt(0).toUpperCase();
});

const userName = computed(() => {
  return userStore.name || t("ai.chat.avatarUser");
});

const effectiveReactSteps = computed(() => props.reactSteps ?? props.message.reactSteps ?? []);

const stepLabels = computed(() => ({
  thought: t("ai.chat.reactThought"),
  action: t("ai.chat.reactAction"),
  observation: t("ai.chat.reactObservation"),
  final: t("ai.chat.reactFinal")
}));

function formatTime(iso: string): string {
  try {
    const date = new Date(iso);
    const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
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
  gap: 16px;
  margin-bottom: 24px;
  align-items: flex-start;
  width: 100%;
}

.chat-message__avatar {
  flex-shrink: 0;
  margin-top: 2px;
}

.chat-message__content-area {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.chat-message__header {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 4px;
}

.chat-message__name {
  font-weight: 600;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
}

.chat-message__time {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.chat-message__bubble {
  width: 100%;
  color: rgba(0, 0, 0, 0.85);
  line-height: 1.6;
}

.user-text {
  white-space: pre-wrap;
  word-break: break-word;
}

.chat-message__stream :deep(.dual-stream-message__reasoning) {
  background: rgba(82, 196, 26, 0.06);
  border-color: rgba(82, 196, 26, 0.16);
}

.chat-message__stream :deep(.dual-stream-message__toggle) {
  color: #389e0d;
}

.chat-message__stream :deep(.dual-stream-message__reasoning-text .markdown-body) {
  color: rgba(0, 0, 0, 0.65);
  font-size: 13px;
}

.chat-message__stream :deep(.dual-stream-message__step-label) {
  color: #389e0d;
}

.chat-message__stream :deep(.dual-stream-message__cursor) {
  color: #52c41a;
}
</style>
