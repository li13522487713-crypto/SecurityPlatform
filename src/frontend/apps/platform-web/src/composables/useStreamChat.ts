import { ref } from "vue";
import type { AgentChatAttachment, ChatMessageDto } from "@/services/api-ai";
import { createAgentChatStream } from "@/services/api-ai";
import {
  useDualStreamRenderer,
  type StreamChatMessage,
  type StreamPhase
} from "@atlas/ai-core";

export interface UseStreamChatOptions {
  /** 使用 getter 以便路由参数变化时仍指向正确 Agent（且避免大整数精度丢失） */
  agentId: () => string;
  enableRag?: () => boolean;
}

export function useStreamChat(options: UseStreamChatOptions) {
  const messages = ref<StreamChatMessage[]>([]);
  const isStreaming = ref(false);
  const error = ref<string | null>(null);
  const currentConversationId = ref<string | null>(null);
  const reasoningText = ref("");
  const answerText = ref("");
  const reactSteps = ref<StreamChatMessage["reactSteps"]>([]);
  const streamPhase = ref<StreamPhase>("idle");

  let abortController: AbortController | null = null;

  function loadHistory(history: ChatMessageDto[]) {
    messages.value = history
      .filter((m) => m.role !== "system")
      .map((m) => ({
        id: m.id,
        role: m.role as "user" | "assistant",
        content: m.content,
        createdAt: m.createdAt,
        reasoningText: "",
        reactSteps: [],
        isStreaming: false,
        isReasoningStreaming: false,
        isAnswerStreaming: false,
        streamPhase: "completed"
      }));
  }

  function clearMessages() {
    messages.value = [];
    currentConversationId.value = null;
    reasoningText.value = "";
    answerText.value = "";
    reactSteps.value = [];
    streamPhase.value = "idle";
  }

  async function sendMessage(text: string, attachments?: AgentChatAttachment[]) {
    const normalizedText = text.trim();
    const normalizedAttachments = (attachments ?? [])
      .filter((item) => item.type && (item.url || item.fileId || item.text));
    if (isStreaming.value || (!normalizedText && normalizedAttachments.length === 0)) return;

    error.value = null;
    reasoningText.value = "";
    answerText.value = "";
    reactSteps.value = [];
    streamPhase.value = "idle";

    const userMsg: StreamChatMessage = {
      id: Date.now(),
      role: "user",
      content: buildUserDisplayText(normalizedText, normalizedAttachments),
      createdAt: new Date().toISOString()
    };
    messages.value.push(userMsg);

    const assistantMsg: StreamChatMessage = {
      id: Date.now() + 1,
      role: "assistant",
      content: "",
      createdAt: new Date().toISOString(),
      reasoningText: "",
      reactSteps: [],
      isStreaming: true,
      isReasoningStreaming: true,
      isAnswerStreaming: false,
      streamPhase: "reasoning"
    };
    messages.value.push(assistantMsg);

    isStreaming.value = true;

    const streamRenderer = useDualStreamRenderer({
      onFlush: (state) => {
        assistantMsg.content = state.answerText;
        assistantMsg.reasoningText = state.reasoningText;
        assistantMsg.reactSteps = [...state.reactSteps];
        assistantMsg.isStreaming = state.isStreaming;
        assistantMsg.isReasoningStreaming = state.isReasoningStreaming;
        assistantMsg.isAnswerStreaming = state.isAnswerStreaming;
        assistantMsg.streamPhase = state.streamPhase;
        reasoningText.value = state.reasoningText;
        answerText.value = state.answerText;
        reactSteps.value = [...state.reactSteps];
        streamPhase.value = state.streamPhase;
      }
    });

    const request = {
      conversationId: currentConversationId.value ?? undefined,
      message: normalizedText,
      enableRag: options.enableRag?.(),
      attachments: normalizedAttachments.length > 0 ? normalizedAttachments : undefined
    };

    const { fetchPromise, abortController: ac } = createAgentChatStream(
      options.agentId(),
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

      await streamRenderer.consumeStream(response.body);

      if (streamRenderer.eventError.value) {
        throw new Error(streamRenderer.eventError.value);
      }
    } catch (err: unknown) {
      if ((err as Error).name === "AbortError") {
        await streamRenderer.stop();
        if (!assistantMsg.content && !assistantMsg.reasoningText && (assistantMsg.reactSteps?.length ?? 0) === 0) {
          messages.value.pop();
        }
      } else {
        await streamRenderer.stop();
        error.value = (err as Error).message || "发送失败";
        messages.value.pop();
      }
    } finally {
      assistantMsg.isStreaming = false;
      assistantMsg.isReasoningStreaming = false;
      assistantMsg.isAnswerStreaming = false;
      assistantMsg.streamPhase = "completed";
      streamPhase.value = "completed";
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
    reasoningText,
    answerText,
    reactSteps,
    streamPhase,
    currentConversationId,
    loadHistory,
    clearMessages,
    sendMessage,
    cancelStream
  };
}

function buildUserDisplayText(text: string, attachments: AgentChatAttachment[]) {
  if (attachments.length === 0) {
    return text;
  }

  const lines = attachments.map((item, index) =>
    `[${index + 1}] ${item.type}${item.name ? `(${item.name})` : ""}`);
  if (!text) {
    return `📎 ${lines.join(", ")}`;
  }

  return `${text}\n\n📎 ${lines.join(", ")}`;
}
