import { ref } from "vue";
import type { ReActEventType, ReActStep } from "../types/chat";

export function useReActStream() {
  const steps = ref<ReActStep[]>([]);

  function reset() {
    steps.value = [];
  }

  function append(eventType: ReActEventType, content: string) {
    if (!content.trim()) {
      return;
    }

    steps.value.push({
      id: `${eventType}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      eventType,
      content,
      createdAt: new Date().toISOString()
    });
  }

  return {
    steps,
    reset,
    append
  };
}
