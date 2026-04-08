export interface StreamChatMessage {
  id: number | string;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: string;
  reasoningText?: string;
  reactSteps?: ReActStep[];
  isStreaming?: boolean;
  isReasoningStreaming?: boolean;
  isAnswerStreaming?: boolean;
  streamPhase?: StreamPhase;
}

export type ReActEventType = "thought" | "action" | "observation" | "final";

export type StreamPhase = "idle" | "reasoning" | "answer" | "completed";

export interface ReActStep {
  id: string;
  eventType: ReActEventType;
  content: string;
  createdAt: string;
}

export interface StreamChunkEvent {
  event: string;
  data: string;
}

export interface StreamReasoningState {
  text: string;
  steps: ReActStep[];
  isStreaming: boolean;
}

export interface StreamMessageState {
  answerText: string;
  reasoningText: string;
  reactSteps: ReActStep[];
  streamPhase: StreamPhase;
  isStreaming: boolean;
  isReasoningStreaming: boolean;
  isAnswerStreaming: boolean;
  eventError: string | null;
}

export interface UseDualStreamRendererResult {
  answerText: { value: string };
  reasoningText: { value: string };
  reactSteps: { value: ReActStep[] };
  streamPhase: { value: StreamPhase };
  isStreaming: { value: boolean };
  isReasoningStreaming: { value: boolean };
  isAnswerStreaming: { value: boolean };
  eventError: { value: string | null };
  snapshot: () => StreamMessageState;
  reset: () => void;
  processChunk: (chunk: StreamChunkEvent) => void;
  consumeStream: (body: ReadableStream<Uint8Array>) => Promise<void>;
  complete: () => Promise<void>;
  stop: () => Promise<void>;
}
