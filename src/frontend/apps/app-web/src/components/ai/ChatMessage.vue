<template>
  <div :class="['chat-msg', `chat-msg--${message.role}`]">
    <template v-if="message.role === 'assistant'">
      <div class="msg-avatar msg-avatar--ai">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
        </svg>
      </div>
      <div class="msg-body msg-body--ai">
        <DualStreamMessage
          class="msg-stream"
          :content="message.content"
          :reasoning-text="message.reasoningText"
          :react-steps="effectiveReactSteps"
          :is-streaming="message.isStreaming"
          :show-typing-cursor="showTypingCursor"
          :reasoning-title="t('ai.chat.thinkPanelTitle')"
          :step-labels="stepLabels"
        />
      </div>
    </template>

    <template v-else>
      <div class="msg-body msg-body--user">
        <div class="msg-bubble-user">
          <div class="user-text">{{ message.content }}</div>
        </div>
      </div>
    </template>
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

const { t } = useI18n();

const props = defineProps<{
  message: StreamChatMessage;
  reactSteps?: ReActStep[];
  showTypingCursor?: boolean;
}>();

const effectiveReactSteps = computed(() => props.reactSteps ?? props.message.reactSteps ?? []);

const stepLabels = computed(() => ({
  thought: t("ai.chat.reactThought"),
  action: t("ai.chat.reactAction"),
  observation: t("ai.chat.reactObservation"),
  final: t("ai.chat.reactFinal")
}));
</script>

<style scoped>
.chat-msg {
  display: flex;
  gap: 12px;
  margin-bottom: 28px;
  width: 100%;
}

.chat-msg--user {
  justify-content: flex-end;
}

.chat-msg--assistant {
  justify-content: flex-start;
}

/* ── AI Avatar ── */
.msg-avatar--ai {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #4f39f6;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  margin-top: 2px;
}

/* ── Message body ── */
.msg-body {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.msg-body--ai {
  flex: 1;
  max-width: calc(100% - 44px);
}

.msg-body--user {
  max-width: 80%;
}

/* ── AI text (no bubble) ── */
.msg-stream {
  color: #1e2939;
  font-size: 15px;
  line-height: 1.7;
}

.msg-stream :deep(.markdown-body) {
  color: #1e2939;
  font-size: 15px;
  line-height: 1.7;
}

/* ── Dark code blocks ── */
.msg-stream :deep(.markdown-body pre) {
  background: #1e1e2e;
  color: #cdd6f4;
  border-radius: 14px;
  border: none;
  overflow: hidden;
}

.msg-stream :deep(.markdown-body pre code) {
  font-family: "Consolas", "SF Mono", "Fira Code", monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #cdd6f4;
}

.msg-stream :deep(.markdown-body code) {
  background: #f3f4f6;
  border-radius: 4px;
  padding: 2px 6px;
  font-size: 13px;
}

.msg-stream :deep(.markdown-body pre code) {
  background: transparent;
  padding: 0;
}

/* ── User bubble ── */
.msg-bubble-user {
  background: #f3f4f6;
  border-radius: 20px 20px 6px 20px;
  padding: 12px 16px;
}

.user-text {
  white-space: pre-wrap;
  word-break: break-word;
  color: #1e2939;
  font-size: 15px;
  line-height: 1.6;
}

.msg-stream :deep(.dual-stream-message__reasoning) {
  background: linear-gradient(180deg, rgba(79, 70, 229, 0.07) 0%, rgba(79, 70, 229, 0.03) 100%);
  border-color: rgba(79, 70, 229, 0.14);
}

.msg-stream :deep(.dual-stream-message__toggle) {
  color: #4f39f6;
}

.msg-stream :deep(.dual-stream-message__reasoning-text .markdown-body) {
  color: #667085;
  font-size: 14px;
}

.msg-stream :deep(.dual-stream-message__step-label) {
  color: #4f39f6;
}

.msg-stream :deep(.dual-stream-message__cursor) {
  color: #4f39f6;
}
</style>
