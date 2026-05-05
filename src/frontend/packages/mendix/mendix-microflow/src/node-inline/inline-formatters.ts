import type { MicroflowTraceFrame } from "../debug/trace-types";

export function formatPrimitive(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  if (typeof value === "string") {
    return value;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

export function truncate(text: string, max = 120): string {
  if (text.length <= max) {
    return text;
  }
  return `${text.slice(0, Math.max(0, max - 1))}…`;
}

export function expressionText(expression: unknown): string {
  if (!expression || typeof expression !== "object") {
    return "";
  }
  const candidate = expression as { raw?: unknown; text?: unknown };
  if (typeof candidate.raw === "string") {
    return candidate.raw;
  }
  if (typeof candidate.text === "string") {
    return candidate.text;
  }
  return "";
}

export function runtimeInputPreview(frame?: MicroflowTraceFrame): string {
  if (!frame?.input) {
    return "";
  }
  return truncate(formatPrimitive(frame.input), 100);
}

export function runtimeOutputPreview(frame?: MicroflowTraceFrame): string {
  if (!frame?.output) {
    return "";
  }
  return truncate(formatPrimitive(frame.output), 100);
}
