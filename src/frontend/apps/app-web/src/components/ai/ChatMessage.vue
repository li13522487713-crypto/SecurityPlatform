<template>
  <div :class="['chat-msg', `chat-msg--${message.role}`]">
    <template v-if="message.role === 'assistant'">
      <div class="msg-avatar msg-avatar--ai">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
        </svg>
      </div>
      <div class="msg-body msg-body--ai">
        <div v-if="reactSteps && reactSteps.length > 0" class="think-panel">
          <button class="think-toggle" @click="thinkExpanded = !thinkExpanded">
            <span class="think-toggle-icon">{{ thinkExpanded ? "▾" : "▸" }}</span>
            {{ t("ai.chat.thinkPanelTitle") }}
          </button>
          <div v-if="thinkExpanded" class="think-content">
            <div v-for="step in reactSteps" :key="step.id" class="think-step">
              <div class="think-step-label">{{ formatReActStep(step.eventType) }}</div>
              <MarkdownRenderer :content="step.content" />
            </div>
          </div>
        </div>
        <div class="msg-text msg-text--ai">
          <MarkdownRenderer :content="message.content" />
          <span v-if="message.isStreaming" class="typing-cursor">▋</span>
        </div>
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
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { MarkdownRenderer, type ReActEventType, type ReActStep } from "@atlas/ai-core";
import type { StreamChatMessage } from "@/composables/useStreamChat";

const { t } = useI18n();

defineProps<{
  message: StreamChatMessage;
  reactSteps?: ReActStep[];
}>();

const thinkExpanded = ref(false);

function formatReActStep(eventType: ReActEventType) {
  switch (eventType) {
    case "thought":
      return t("ai.chat.reactThought");
    case "action":
      return t("ai.chat.reactAction");
    case "observation":
      return t("ai.chat.reactObservation");
    case "final":
      return t("ai.chat.reactFinal");
    default:
      return eventType;
  }
}
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
.msg-text--ai {
  color: #1e2939;
  font-size: 15px;
  line-height: 1.7;
}

.msg-text--ai :deep(.markdown-body) {
  color: #1e2939;
  font-size: 15px;
  line-height: 1.7;
}

/* ── Dark code blocks ── */
.msg-text--ai :deep(.markdown-body pre) {
  background: #1e1e2e;
  color: #cdd6f4;
  border-radius: 14px;
  border: none;
  overflow: hidden;
}

.msg-text--ai :deep(.markdown-body pre code) {
  font-family: "Consolas", "SF Mono", "Fira Code", monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #cdd6f4;
}

.msg-text--ai :deep(.markdown-body code) {
  background: #f3f4f6;
  border-radius: 4px;
  padding: 2px 6px;
  font-size: 13px;
}

.msg-text--ai :deep(.markdown-body pre code) {
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

/* ── Think panel (ReAct) ── */
.think-panel {
  margin-bottom: 12px;
}

.think-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 20px;
  padding: 4px 14px;
  font-size: 13px;
  color: #6a7282;
  cursor: pointer;
  transition: all 0.15s;
}

.think-toggle:hover {
  background: #f9fafb;
}

.think-toggle-icon {
  font-size: 11px;
}

.think-content {
  margin-top: 8px;
  border-left: 3px solid #e0e7ff;
  padding-left: 14px;
}

.think-step {
  margin-bottom: 8px;
}

.think-step-label {
  font-weight: 600;
  font-size: 12px;
  color: #4f39f6;
  margin-bottom: 2px;
}

.think-step :deep(.markdown-body) {
  font-size: 14px;
  color: #6a7282;
  line-height: 1.6;
}

/* ── Typing cursor ── */
@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

.typing-cursor {
  display: inline-block;
  animation: blink 1s infinite;
  color: #4f39f6;
  vertical-align: baseline;
}
</style>
