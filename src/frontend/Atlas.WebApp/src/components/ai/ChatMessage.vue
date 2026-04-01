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
          <div v-if="reactSteps && reactSteps.length > 0" class="inline-react-steps">
            <a-collapse size="small" :bordered="false" class="react-collapse">
              <a-collapse-panel key="react" :header="t('ai.chat.reactPanelTitle')">
                <a-timeline>
                  <a-timeline-item v-for="step in reactSteps" :key="step.id">
                    <div class="react-step-title">{{ formatReActStep(step.eventType) }}</div>
                    <pre class="react-step-content">{{ step.content }}</pre>
                  </a-timeline-item>
                </a-timeline>
              </a-collapse-panel>
            </a-collapse>
          </div>
          <MarkdownRenderer :content="message.content" />
          <span v-if="message.isStreaming" class="typing-cursor">▋</span>
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
import MarkdownRenderer from "./MarkdownRenderer.vue";
import type { StreamChatMessage } from "@/composables/useStreamChat";
import type { ReActStep, ReActEventType } from "@/composables/useReActStream";
import { getActiveLocale } from "@/i18n";
import { useUserStore } from "@/stores/user";

const { t } = useI18n();
const userStore = useUserStore();

const props = defineProps<{
  message: StreamChatMessage;
  userInitial?: string;
  reactSteps?: ReActStep[];
}>();

const userInitial = computed(() => {
  const name = userStore.name || "U";
  return name.charAt(0).toUpperCase();
});

const userName = computed(() => {
  return userStore.name || t("ai.chat.avatarUser");
});

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

.inline-react-steps {
  margin-bottom: 12px;
}

.react-collapse {
  background: transparent;
}

.react-collapse :deep(.ant-collapse-header) {
  padding: 4px 0 !important;
  color: rgba(0, 0, 0, 0.45) !important;
  font-size: 13px;
}

.react-collapse :deep(.ant-collapse-content-box) {
  padding: 8px 0 0 0 !important;
}

.react-step-title {
  font-weight: 600;
  margin-bottom: 4px;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
}

.react-step-content {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.65);
  background: #f5f5f5;
  padding: 8px 12px;
  border-radius: 6px;
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

.typing-cursor {
  display: inline-block;
  animation: blink 1s infinite;
  color: #52c41a;
  vertical-align: baseline;
}
</style>
