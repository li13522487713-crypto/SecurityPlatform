import type { AgentChatAttachment, ChatMessageDto } from "@/services/api-ai";
import { createAgentChatStream } from "@/services/api-ai";
import { useStreamChatShared } from "@atlas/shared-core/composables";

export interface UseStreamChatOptions {
  agentId: () => string;
  enableRag?: () => boolean;
}

export function useStreamChat(options: UseStreamChatOptions) {
  const shared = useStreamChatShared<AgentChatAttachment>({
    enableRag: options.enableRag,
    createStream: ({ conversationId, message, attachments }) => {
      return createAgentChatStream(
        options.agentId(),
        {
          conversationId,
          message,
          enableRag: options.enableRag?.(),
          attachments
        },
        "react"
      );
    }
  });

  function loadHistory(history: ChatMessageDto[]) {
    shared.loadHistory(history);
  }

  return {
    ...shared,
    loadHistory
  };
}
