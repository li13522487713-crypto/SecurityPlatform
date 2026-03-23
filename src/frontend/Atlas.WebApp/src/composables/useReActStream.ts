import { ref } from "vue";

export type ReActEventType = "thought" | "action" | "observation" | "final";

export interface ReActStep {
  id: string;
  eventType: ReActEventType;
  content: string;
  createdAt: string;
}

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
