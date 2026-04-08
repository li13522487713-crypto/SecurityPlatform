import { ref } from "vue";
import { parseSseStream } from "./useSseParser";
import type {
  ReActEventType,
  ReActStep,
  StreamChunkEvent,
  StreamMessageState,
  StreamPhase,
  UseDualStreamRendererResult
} from "../types/chat";

interface UseDualStreamRendererOptions {
  onFlush?: (state: StreamMessageState) => void;
}

const FRAME_FALLBACK_MS = 16;

export function useDualStreamRenderer(options: UseDualStreamRendererOptions = {}): UseDualStreamRendererResult {
  const answerText = ref("");
  const reasoningText = ref("");
  const reactSteps = ref<ReActStep[]>([]);
  const streamPhase = ref<StreamPhase>("idle");
  const isStreaming = ref(false);
  const isReasoningStreaming = ref(false);
  const isAnswerStreaming = ref(false);
  const eventError = ref<string | null>(null);

  let reasoningDraft = "";
  let answerFallbackDraft = "";
  let answerFinalDraft = "";
  let hasFinalAnswer = false;
  let pendingSteps: ReActStep[] = [];
  let frameHandle: number | null = null;
  let timeoutHandle: ReturnType<typeof setTimeout> | null = null;

  function snapshot(): StreamMessageState {
    return {
      answerText: answerText.value,
      reasoningText: reasoningText.value,
      reactSteps: reactSteps.value,
      streamPhase: streamPhase.value,
      isStreaming: isStreaming.value,
      isReasoningStreaming: isReasoningStreaming.value,
      isAnswerStreaming: isAnswerStreaming.value,
      eventError: eventError.value
    };
  }

  function setPhase(phase: StreamPhase) {
    streamPhase.value = phase;
    isStreaming.value = phase === "reasoning" || phase === "answer";
    isReasoningStreaming.value = phase === "reasoning";
    isAnswerStreaming.value = phase === "answer";
  }

  function cancelScheduledFlush() {
    if (typeof window !== "undefined" && frameHandle !== null) {
      window.cancelAnimationFrame(frameHandle);
      frameHandle = null;
    }
    if (timeoutHandle !== null) {
      clearTimeout(timeoutHandle);
      timeoutHandle = null;
    }
  }

  function emitFlush() {
    answerText.value = hasFinalAnswer ? answerFinalDraft : answerFallbackDraft;
    reasoningText.value = reasoningDraft;
    if (pendingSteps.length > 0) {
      reactSteps.value = [...reactSteps.value, ...pendingSteps];
      pendingSteps = [];
    }
    options.onFlush?.(snapshot());
  }

  function flushNow() {
    cancelScheduledFlush();
    emitFlush();
  }

  function scheduleFlush() {
    if (frameHandle !== null || timeoutHandle !== null) {
      return;
    }

    if (typeof window !== "undefined" && typeof window.requestAnimationFrame === "function") {
      frameHandle = window.requestAnimationFrame(() => {
        frameHandle = null;
        emitFlush();
      });
      return;
    }

    timeoutHandle = setTimeout(() => {
      timeoutHandle = null;
      emitFlush();
    }, FRAME_FALLBACK_MS);
  }

  function reset() {
    cancelScheduledFlush();
    reasoningDraft = "";
    answerFallbackDraft = "";
    answerFinalDraft = "";
    hasFinalAnswer = false;
    pendingSteps = [];
    answerText.value = "";
    reasoningText.value = "";
    reactSteps.value = [];
    eventError.value = null;
    setPhase("idle");
    options.onFlush?.(snapshot());
  }

  function pushStep(eventType: ReActEventType, content: string) {
    pendingSteps.push({
      id: `${eventType}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      eventType,
      content,
      createdAt: new Date().toISOString()
    });
  }

  function appendAnswerChunk(content: string, useFinalAnswer: boolean) {
    if (useFinalAnswer) {
      hasFinalAnswer = true;
      answerFinalDraft += content;
    } else if (!hasFinalAnswer) {
      answerFallbackDraft += content;
    }
  }

  function processChunk(chunk: StreamChunkEvent) {
    const normalizedData = chunk.data ?? "";
    if (!normalizedData) {
      return;
    }

    switch (chunk.event) {
      case "thought":
        if (streamPhase.value === "idle") {
          setPhase("reasoning");
        }
        reasoningDraft += normalizedData;
        pushStep("thought", normalizedData);
        break;
      case "action":
      case "observation":
        if (streamPhase.value === "idle") {
          setPhase("reasoning");
        }
        pushStep(chunk.event as ReActEventType, normalizedData);
        break;
      case "final":
        setPhase("answer");
        pushStep("final", normalizedData);
        appendAnswerChunk(normalizedData, true);
        break;
      case "error":
        eventError.value = normalizedData;
        break;
      case "data":
      default:
        if (streamPhase.value === "idle") {
          setPhase("answer");
        }
        appendAnswerChunk(normalizedData, false);
        break;
    }

    scheduleFlush();
  }

  async function consumeStream(body: ReadableStream<Uint8Array>) {
    reset();
    setPhase("reasoning");

    for await (const chunk of parseSseStream(body)) {
      processChunk(chunk);
    }

    await complete();
  }

  async function complete() {
    flushNow();
    setPhase("completed");
    options.onFlush?.(snapshot());
  }

  async function stop() {
    flushNow();
    if (streamPhase.value !== "idle") {
      setPhase("completed");
      options.onFlush?.(snapshot());
    }
  }

  return {
    answerText,
    reasoningText,
    reactSteps,
    streamPhase,
    isStreaming,
    isReasoningStreaming,
    isAnswerStreaming,
    eventError,
    snapshot,
    reset,
    processChunk,
    consumeStream,
    complete,
    stop
  };
}
