export function getValueByPath(source: Record<string, unknown>, path: string): unknown {
  if (!path || path === "root") {
    return source;
  }
  const segments = path.split(".");
  let current: unknown = source;
  for (const segment of segments) {
    if (!current || typeof current !== "object") {
      return undefined;
    }
    current = (current as Record<string, unknown>)[segment];
  }
  return current;
}

export function setValueByPath(source: Record<string, unknown>, path: string, value: unknown): Record<string, unknown> {
  if (!path || path === "root") {
    return source;
  }
  const segments = path.split(".");
  const next = { ...source };
  let current: Record<string, unknown> = next;

  for (let index = 0; index < segments.length; index += 1) {
    const segment = segments[index];
    if (index === segments.length - 1) {
      current[segment] = value;
      break;
    }
    const existing = current[segment];
    if (!existing || typeof existing !== "object" || Array.isArray(existing)) {
      current[segment] = {};
    } else {
      current[segment] = { ...(existing as Record<string, unknown>) };
    }
    current = current[segment] as Record<string, unknown>;
  }
  return next;
}

