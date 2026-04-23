export function formatPublishLogPayload(payload: unknown): string {
  if (payload === null || payload === undefined) {
    return "-";
  }

  if (typeof payload === "string") {
    return payload.trim() || "-";
  }

  try {
    return JSON.stringify(payload, null, 2);
  } catch {
    return String(payload);
  }
}

export function buildPublishLogPreview(payload: unknown, maxLength = 160): string {
  const text = formatPublishLogPayload(payload);
  if (text === "-" || text.length <= maxLength) {
    return text;
  }

  return `${text.slice(0, maxLength)}...`;
}
