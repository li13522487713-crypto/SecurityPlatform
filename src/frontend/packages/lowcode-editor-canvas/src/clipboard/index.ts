/**
 * 画布剪贴板（M04 C04-5）。
 *
 * 支持：复制/剪切/粘贴/同位粘贴/跨页面粘贴/跨应用粘贴。
 * 实现策略：
 * - 内存剪贴板（默认）：进程内单例，不污染浏览器系统剪贴板。
 * - 系统剪贴板（按需）：序列化为 JSON 文本写入；粘贴时反序列化（用于跨应用粘贴）。
 */

import type { ComponentSchema } from '@atlas/lowcode-schema';
import { ComponentSchemaZod } from '@atlas/lowcode-schema';

export interface ClipboardPayload {
  /** 复制的组件子树（可多个根，互不嵌套）。*/
  components: ComponentSchema[];
  /** 来源应用 ID（跨应用粘贴时用于审计）。*/
  sourceAppId?: string;
  /** 来源页面 ID。*/
  sourcePageId?: string;
  /** 剪贴时间戳。*/
  copiedAt: number;
}

let memory: ClipboardPayload | null = null;

export function copyToClipboard(payload: ClipboardPayload): void {
  memory = payload;
}

export function readFromClipboard(): ClipboardPayload | null {
  return memory ? { ...memory, components: memory.components.map((c) => structuredClone(c)) } : null;
}

export function clearClipboard(): void {
  memory = null;
}

const CLIPBOARD_MIME = 'application/x-atlas-lowcode-clipboard+json';

/** 写入系统剪贴板（用于跨应用粘贴）。需要 navigator.clipboard 可用。*/
export async function writeToSystemClipboard(payload: ClipboardPayload): Promise<void> {
  const json = JSON.stringify(payload);
  if (typeof navigator === 'undefined' || !('clipboard' in navigator)) return;
  await navigator.clipboard.writeText(json);
}

/** 从系统剪贴板读出，并经 zod 校验后返回。*/
export async function readFromSystemClipboard(): Promise<ClipboardPayload | null> {
  if (typeof navigator === 'undefined' || !('clipboard' in navigator)) return null;
  const text = await navigator.clipboard.readText();
  try {
    const parsed = JSON.parse(text) as ClipboardPayload;
    if (!parsed || !Array.isArray(parsed.components)) return null;
    // 校验每个组件子树
    for (const c of parsed.components) {
      const r = ComponentSchemaZod.safeParse(c);
      if (!r.success) return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export const __CLIPBOARD_MIME = CLIPBOARD_MIME;
