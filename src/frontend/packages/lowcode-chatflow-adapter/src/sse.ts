/**
 * SSE EventStream 解析（M11 C11-1）。
 *
 * 与服务端约定：每帧 data: <json>\n\n。
 * 解析器：把任意 ReadableStream<Uint8Array> 转为 AsyncIterable<ChatChunk>。
 */

import type { ChatChunk } from './types';

export async function* parseSseStream(stream: ReadableStream<Uint8Array>): AsyncIterable<ChatChunk> {
  const decoder = new TextDecoder();
  const reader = stream.getReader();
  let buffer = '';
  let seq = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let idx: number;
      while ((idx = buffer.indexOf('\n\n')) >= 0) {
        const frame = buffer.slice(0, idx);
        buffer = buffer.slice(idx + 2);
        const chunk = parseFrame(frame, ++seq);
        if (chunk) yield chunk;
      }
    }
    // 末帧
    if (buffer.trim().length > 0) {
      const chunk = parseFrame(buffer, ++seq);
      if (chunk) yield chunk;
    }
  } finally {
    try { reader.releaseLock(); } catch { /* noop */ }
  }
}

function parseFrame(frame: string, seq: number): ChatChunk | null {
  const lines = frame.split('\n');
  let dataLine = '';
  for (const ln of lines) {
    if (ln.startsWith('data:')) dataLine += ln.slice(5).trimStart();
  }
  if (!dataLine) return null;
  try {
    const obj = JSON.parse(dataLine) as ChatChunk;
    return { ...obj, seq };
  } catch {
    return { kind: 'error', message: `非法 SSE 帧: ${dataLine}`, seq } as ChatChunk;
  }
}
