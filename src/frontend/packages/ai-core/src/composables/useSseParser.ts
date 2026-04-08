/**
 * Parses a ReadableStream of SSE (Server-Sent Events) data.
 * Yields `{ event, data }` tuples for each complete SSE event.
 * Returns when the stream ends or a `[DONE]` sentinel is received.
 */
export async function* parseSseStream(
  body: ReadableStream<Uint8Array>
): AsyncGenerator<{ event: string; data: string }> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let currentEventType = "data";
  let currentDataLines: string[] = [];

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() ?? "";

      for (const line of lines) {
        if (line.length === 0) {
          if (currentDataLines.length > 0) {
            const eventData = currentDataLines.join("\n").trim();
            currentDataLines = [];
            const eventType = currentEventType;
            currentEventType = "data";

            if (eventData && eventData !== "[DONE]") {
              yield { event: eventType, data: eventData };
            }
            if (eventData === "[DONE]") {
              return;
            }
          } else {
            currentEventType = "data";
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

    if (currentDataLines.length > 0) {
      const eventData = currentDataLines.join("\n").trim();
      if (eventData && eventData !== "[DONE]") {
        yield { event: currentEventType, data: eventData };
      }
    }
  } finally {
    reader.releaseLock();
  }
}
