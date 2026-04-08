<template>
  <div class="dual-stream-message">
    <div v-if="hasReasoning" class="dual-stream-message__reasoning">
      <button type="button" class="dual-stream-message__toggle" @click="toggleExpanded">
        <span class="dual-stream-message__toggle-icon">{{ expanded ? "▾" : "▸" }}</span>
        <span class="dual-stream-message__toggle-text">{{ reasoningTitle }}</span>
      </button>

      <div v-if="expanded" class="dual-stream-message__reasoning-body">
        <div v-if="reasoningText" class="dual-stream-message__reasoning-text">
          <MarkdownRenderer :content="reasoningText" :streaming="isStreaming" />
        </div>
        <div v-if="reactSteps.length > 0" class="dual-stream-message__steps">
          <div v-for="step in reactSteps" :key="step.id" class="dual-stream-message__step">
            <div class="dual-stream-message__step-label">{{ formatStepLabel(step.eventType) }}</div>
            <MarkdownRenderer :content="step.content" />
          </div>
        </div>
      </div>
    </div>

    <div class="dual-stream-message__answer">
      <MarkdownRenderer :content="content" :streaming="isStreaming" />
      <span v-if="showTypingCursor && isStreaming" class="dual-stream-message__cursor">▋</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import MarkdownRenderer from "./MarkdownRenderer.vue";
import type { ReActEventType, ReActStep } from "../types/chat";

const props = withDefaults(defineProps<{
  content: string;
  reasoningText?: string;
  reactSteps?: ReActStep[];
  isStreaming?: boolean;
  showTypingCursor?: boolean;
  reasoningTitle: string;
  stepLabels?: Partial<Record<ReActEventType, string>>;
}>(), {
  reasoningText: "",
  reactSteps: () => [],
  isStreaming: false,
  showTypingCursor: false,
  stepLabels: () => ({})
});

const expanded = ref(false);

const hasReasoning = computed(() =>
  Boolean(props.reasoningText?.trim()) || props.reactSteps.length > 0
);

watch(
  () => [props.isStreaming, props.reasoningText, props.reactSteps.length] as const,
  ([streaming, reasoning, stepCount]) => {
    if (streaming && (Boolean(reasoning?.trim()) || stepCount > 0)) {
      expanded.value = true;
    }
  },
  { immediate: true }
);

function toggleExpanded() {
  expanded.value = !expanded.value;
}

function formatStepLabel(eventType: ReActEventType) {
  return props.stepLabels[eventType] ?? eventType;
}
</script>

<style scoped>
.dual-stream-message {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.dual-stream-message__reasoning {
  border: 1px solid rgba(79, 70, 229, 0.14);
  background: rgba(79, 70, 229, 0.04);
  border-radius: 16px;
  overflow: hidden;
}

.dual-stream-message__toggle {
  width: 100%;
  border: none;
  background: transparent;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  cursor: pointer;
  color: #4f46e5;
  font-size: 13px;
  font-weight: 600;
}

.dual-stream-message__toggle-icon {
  font-size: 12px;
  line-height: 1;
}

.dual-stream-message__reasoning-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 0 14px 14px;
}

.dual-stream-message__reasoning-text {
  color: #4b5563;
}

.dual-stream-message__steps {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.dual-stream-message__step {
  padding-top: 10px;
  border-top: 1px dashed rgba(79, 70, 229, 0.16);
}

.dual-stream-message__step:first-child {
  padding-top: 0;
  border-top: none;
}

.dual-stream-message__step-label {
  margin-bottom: 4px;
  color: #4f46e5;
  font-size: 12px;
  font-weight: 600;
}

.dual-stream-message__answer {
  min-width: 0;
  color: inherit;
}

.dual-stream-message__cursor {
  display: inline-block;
  color: currentColor;
  animation: dual-stream-cursor-blink 1s infinite;
  vertical-align: baseline;
}

@keyframes dual-stream-cursor-blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}
</style>
