import { ref } from "vue";
import type { ChatMessageDto } from "@/services/api-conversation";
import { createAgentChatStream } from "@/services/api-conversation";
import { useReActStream, type ReActEventType } from "@/composables/useReActStream";

export interface StreamChatMessage {
  id: number;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: string;
  isStreaming?: boolean;
}

export interface UseStreamChatOptions {
  agentId: number;
  enableRag?: () => boolean;
}

export function useStreamChat(options: UseStreamChatOptions) {
  const messages = ref<StreamChatMessage[]>([]);
  const isStreaming = ref(false);
  const error = ref<string | null>(null);
  const currentConversationId = ref<number | null>(null);
  const reactStream = useReActStream();

  let abortController: AbortController | null = null;

  function loadHistory(history: ChatMessageDto[]) {
    messages.value = history
      .filter((m) => m.role !== "system")
      .map((m) => ({
        id: m.id,
        role: m.role as "user" | "assistant",
        content: m.content,
        createdAt: m.createdAt,
        isStreaming: false
      }));
  }

  function clearMessages() {
    messages.value = [];
    currentConversationId.value = null;
  }

  async function sendMessage(text: string) {
    if (isStreaming.value || !text.trim()) return;

    error.value = null;
    reactStream.reset();

    const userMsg: StreamChatMessage = {
      id: Date.now(),
      role: "user",
      content: text.trim(),
      createdAt: new Date().toISOString()
    };
    messages.value.push(userMsg);

    const assistantMsg: StreamChatMessage = {
      id: Date.now() + 1,
      role: "assistant",
      content: "",
      createdAt: new Date().toISOString(),
      isStreaming: true
    };
    messages.value.push(assistantMsg);

    isStreaming.value = true;

    const request = {
      conversationId: currentConversationId.value ?? undefined,
      message: text.trim(),
      enableRag: options.enableRag?.()
    };

    const { fetchPromise, abortController: ac } = createAgentChatStream(
      options.agentId,
      request,
      "react"
    );
    abortController = ac;

    try {
      const response = await fetchPromise;
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      if (!response.body) {
        throw new Error("Response body is null");
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let receivedDone = false;
      let currentEventType = "data";
      let currentDataLines: string[] = [];

      const flushCurrentEvent = () => {
        if (currentDataLines.length === 0) {
          currentEventType = "data";
          return;
        }

        const eventData = currentDataLines.join("\n").trim();
        currentDataLines = [];
        const eventType = currentEventType;
        currentEventType = "data";

        if (!eventData) {
          return;
        }

        if (eventData === "[DONE]") {
          assistantMsg.isStreaming = false;
          receivedDone = true;
          return;
        }

        if (eventType === "thought" || eventType === "action" || eventType === "observation") {
          reactStream.append(eventType as ReActEventType, eventData);
          return;
        }

        if (eventType === "final") {
          reactStream.append("final", eventData);
          if (assistantMsg.content.trim()) {
            assistantMsg.content = eventData;
          } else {
            assistantMsg.content += eventData;
          }
          return;
        }

        assistantMsg.content += eventData;
      };

      while (!receivedDone) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() ?? "";

        for (const line of lines) {
          if (line.length === 0) {
            flushCurrentEvent();
            if (receivedDone) {
              break;
            }
            continue;
          }

          if (line.startsWith("event:")) {
            currentEventType = line.slice("event:".length).trim() || "data";
            continue;
          }

          if (line.startsWith("data:")) {
            currentDataLines.push(line.slice("data:".length).trim());
          }
        }
      }

      if (!receivedDone) {
        flushCurrentEvent();
      }

      if (receivedDone) {
        await reader.cancel();
      }

      assistantMsg.isStreaming = false;
    } catch (err: unknown) {
      assistantMsg.isStreaming = false;
      if ((err as Error).name === "AbortError") {
        if (!assistantMsg.content) {
          messages.value.pop();
        }
      } else {
        error.value = (err as Error).message || "发送失败";
        messages.value.pop();
      }
    } finally {
      isStreaming.value = false;
      abortController = null;
    }
  }

  function cancelStream() {
    if (abortController) {
      abortController.abort();
      abortController = null;
    }
  }

  return {
    messages,
    isStreaming,
    error,
    reactSteps: reactStream.steps,
    currentConversationId,
    loadHistory,
    clearMessages,
    sendMessage,
    cancelStream
  };
}
