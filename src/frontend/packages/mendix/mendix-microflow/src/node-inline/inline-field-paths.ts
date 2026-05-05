export function nodeDataPath(path: string): string {
  return `data.${path}`;
}

export function edgeDataPath(path: string): string {
  return `data.${path}`;
}

export function getByPath(target: unknown, path: string): unknown {
  if (!path) {
    return target;
  }
  return path.split(".").reduce<unknown>((acc, key) => {
    if (!acc || typeof acc !== "object") {
      return undefined;
    }
    return (acc as Record<string, unknown>)[key];
  }, target);
}

export function setByPathImmutable<T extends object>(target: T, path: string, value: unknown): T {
  const keys = path.split(".").filter(Boolean);
  if (keys.length === 0) {
    return target;
  }
  const root = (Array.isArray(target) ? [...target] : { ...(target as Record<string, unknown>) }) as Record<string, unknown>;
  let cursor: Record<string, unknown> = root;
  keys.forEach((key, index) => {
    if (index === keys.length - 1) {
      cursor[key] = value;
      return;
    }
    const current = cursor[key];
    const next =
      current && typeof current === "object"
        ? Array.isArray(current)
          ? [...current]
          : { ...(current as Record<string, unknown>) }
        : {};
    cursor[key] = next;
    cursor = next as Record<string, unknown>;
  });
  return root as T;
}
