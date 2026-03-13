import { ref } from "vue";
import type { ChatMessageDto } from "@/services/api-conversation";
import { createAgentChatStream } from "@/services/api-conversation";

export interface StreamChatMessage {
  id: number;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: string;
  isStreaming?: boolean;
}

export interface UseStreamChatOptions {
  agentId: number;
  enableRag?: boolean;
}

export function useStreamChat(options: UseStreamChatOptions) {
  const messages = ref<StreamChatMessage[]>([]);
  const isStreaming = ref(false);
  const error = ref<string | null>(null);
  const currentConversationId = ref<number | null>(null);

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
      enableRag: options.enableRag
    };

    const { fetchPromise, abortController: ac } = createAgentChatStream(
      options.agentId,
      request
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

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          const data = line.slice(6).trim();
          if (data === "[DONE]") {
            assistantMsg.isStreaming = false;
            break;
          }
          if (data) {
            assistantMsg.content += data;
          }
        }
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
    currentConversationId,
    loadHistory,
    clearMessages,
    sendMessage,
    cancelStream
  };
}
