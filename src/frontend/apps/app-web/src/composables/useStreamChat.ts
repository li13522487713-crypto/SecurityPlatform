import type { AgentChatAttachment, ChatMessageDto } from "@/services/api-conversation";
import { createAgentChatStream } from "@/services/api-conversation";
import { useStreamChatShared } from "@atlas/shared-core/composables";

export interface UseStreamChatOptions {
  appKey: () => string;
  agentId: () => string;
  enableRag?: () => boolean;
}

export function useStreamChat(options: UseStreamChatOptions) {
  const shared = useStreamChatShared<AgentChatAttachment>({
    enableRag: options.enableRag,
    createStream: ({ conversationId, message, attachments }) => {
      return createAgentChatStream(
        options.appKey(),
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
