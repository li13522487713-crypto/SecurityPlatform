import { describe, it, expect } from 'vitest';
import { parseSseStream } from '../sse';
import type { ChatChunk, MessageChunk, FinalChunk, ErrorChunk, ToolCallChunk } from '../types';

function makeStream(text: string): ReadableStream<Uint8Array> {
  const enc = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      controller.enqueue(enc.encode(text));
      controller.close();
    }
  });
}

async function collect(iter: AsyncIterable<ChatChunk>): Promise<ChatChunk[]> {
  const out: ChatChunk[] = [];
  for await (const c of iter) out.push(c);
  return out;
}

describe('parseSseStream', () => {
  it('解析 4 类事件', async () => {
    const text = [
      'data: {"kind":"tool_call","toolName":"search","args":{"q":"hi"}}',
      'data: {"kind":"message","content":"hi"}',
      'data: {"kind":"message","content":" world"}',
      'data: {"kind":"final","outputs":{"final":"hi world"}}'
    ].map((l) => l + '\n\n').join('');
    const chunks = await collect(parseSseStream(makeStream(text)));
    expect(chunks.length).toBe(4);
    expect((chunks[0] as ToolCallChunk).toolName).toBe('search');
    expect((chunks[1] as MessageChunk).content).toBe('hi');
    expect((chunks[3] as FinalChunk).outputs?.final).toBe('hi world');
    chunks.forEach((c, i) => expect(c.seq).toBe(i + 1));
  });

  it('非法 JSON 帧产出 error', async () => {
    const text = 'data: not-a-json\n\n';
    const chunks = await collect(parseSseStream(makeStream(text)));
    expect(chunks[0].kind).toBe('error');
    expect((chunks[0] as ErrorChunk).message).toContain('非法 SSE 帧');
  });

  it('多行 data 拼接', async () => {
    const text = 'data: {"kind":"message",\ndata: "content":"hi"}\n\n';
    const chunks = await collect(parseSseStream(makeStream(text)));
    expect((chunks[0] as MessageChunk).content).toBe('hi');
  });
});
